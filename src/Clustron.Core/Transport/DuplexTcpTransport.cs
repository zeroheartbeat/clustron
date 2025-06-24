// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Discovery;
using Clustron.Core.Helpers;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Clustron.Core.Transport;

public class DuplexTcpTransport : BaseTcpTransport
{
    private readonly int _port;
    private readonly IDiscoveryProvider _discoveryProvider;
    private readonly RetryOptions _retryOptions;
    private readonly ClusterPeerManager _peerManager;
    private TcpListener? _listener;
    private readonly IMetricContributor _metrics;


    public DuplexTcpTransport(
        int port,
        IClusterRuntime clusterRuntime,
        IMessageSerializer serializer,
        IMetricContributor metrics,
        IClusterLoggerProvider loggerProvider,
        RetryOptions retryOptions)
        : base(clusterRuntime, serializer, loggerProvider.GetLogger<DuplexTcpTransport>())
    {
        _port = port;
        _peerManager = clusterRuntime.PeerManager;
        _metrics = metrics;
        _retryOptions = retryOptions;
    }

    public override Task StartAsync(IMessageRouter router)
    {
        _logger.LogInformation("TCP listener starting on port {Port}", _port);

        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            _logger.LogInformation("TCP listener started on port {Port}", _port);

            while (true)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, router);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in TCP listener loop");
                }
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleClientAsync(TcpClient client, IMessageRouter router)
    {
        using var stream = client.GetStream();
        string? senderId = null;

        try
        {
            while (true)
            {
                var lengthBuffer = ArrayPool<byte>.Shared.Rent(4);
                try
                {
                    int read = await stream.ReadAsync(lengthBuffer, 0, 4);
                    if (read == 0)
                    {
                        _logger.LogError("HandleClientAsync: Stream read returned 0. Remote: {RemoteEndPoint}, Local: {LocalEndPoint}",
                            ((IPEndPoint)client.Client.RemoteEndPoint)?.ToString(),
                            ((IPEndPoint)client.Client.LocalEndPoint)?.ToString());

                        break;
                    }

                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    if (length <= 0 || length > 1024 * 10)
                    {
                        _logger.LogCritical("HandleClientAsync: Invalid message length received: {Length}", length);
                        break;
                    }

                    var payloadBuffer = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        await ReadExactlyAsync(stream, payloadBuffer, 0, length);
                        var payload = payloadBuffer.AsSpan(0, length).ToArray();
                        var message = _serializer.Deserialize<Message>(payload);

                        if (message == null) continue;

                        senderId = message.SenderId;

                        if (!string.IsNullOrEmpty(message.CorrelationId) &&
                            _responseAwaiters.TryGetValue(message.CorrelationId, out var tcs))
                        {
                            tcs.TrySetResult(message);
                            _responseAwaiters.TryRemove(message.CorrelationId, out _);
                        }
                        else
                        {
                            if(message.MessageType == MessageTypes.NodeLeft)
                                _logger.LogCritical("Something unexpected happened. CorrelationId: {CorrelationId} for message {message}", message.CorrelationId, message.ToString());
                            await router.RouteAsync(message);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(payloadBuffer);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(lengthBuffer);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Inbound connection failed: {Error}", ex.ToString());
        }
        finally
        {
            if (!string.IsNullOrEmpty(senderId))
            {
                await HandlePeerDownAsync(senderId); // triggers proper peer removal
            }

            client.Close();
        }
    }

    public override async Task SendAsync(NodeInfo target, Message message)
    {
        var body = _serializer.Serialize(message);
        var length = BitConverter.GetBytes(body.Length);

        var sendLock = _sendLocks.GetOrAdd(target.NodeId, _ => new SemaphoreSlim(1, 1));
        await sendLock.WaitAsync();
        try
        {
            await RetryHelper.RetryAsync(async () =>
            {
                var conn = await GetOrCreateConnectionAsync(target);
                if (conn?.IsConnected != true)
                    throw new IOException("Connection not available");

                conn.LastUsedUtc = DateTime.UtcNow;
                var stream = conn.Stream;

                await stream.WriteAsync(length, 0, length.Length);
                await stream.WriteAsync(body, 0, body.Length);
            }, _retryOptions.MaxAttempts, _retryOptions.DelayMilliseconds, _logger);

            _metrics.Increment(MetricKeys.Messages.Sent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send message to {NodeId}: {Message}", target.NodeId, ex.Message);
            _connections.TryRemove(target.NodeId, out var failedConn);
            failedConn?.Dispose();
        }
        finally
        {
            sendLock.Release();
        }
    }

    private async Task<PersistentConnection?> GetOrCreateConnectionAsync(NodeInfo target)
    {
        if (_connections.TryGetValue(target.NodeId, out var existingConn) && existingConn.IsConnected)
        {
            existingConn.LastUsedUtc = DateTime.UtcNow;
            return existingConn;
        }

        try
        {
            _logger.LogDebug("Connecting to {NodeId} at {Host}:{Port}", target.NodeId, target.Host, target.Port);
            var client = new TcpClient();
            await client.ConnectAsync(target.Host, target.Port);

            var persistent = new PersistentConnection(client)
            {
                RemoteNodeId = target.NodeId,
                IsInbound = false
            };

            _connections[target.NodeId] = persistent;

            _logger.LogInformation("Outbound connection to {NodeId} established", target.NodeId);
            return persistent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to connect to {NodeId}: {Error}", target.NodeId, ex.Message);
            return null;
        }
    }

    public override async Task<bool> CanReachNodeAsync(NodeInfo node)
    {
        try
        {
            var conn = await GetOrCreateConnectionAsync(node);
            if (conn?.IsConnected == true)
            {
                conn.LastUsedUtc = DateTime.UtcNow;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public override void RemoveConnection(string nodeId)
    {
        if (_connections.TryRemove(nodeId, out var conn))
        {
            conn.Dispose();
            _logger.LogInformation("Removed connection to {NodeId}", nodeId);
        }
    }

    public override async Task HandlePeerDownAsync(string nodeId)
    {
        var node = _peerManager.GetAllKnownPeers().FirstOrDefault(n => n.NodeId == nodeId);
        if (node == null)
        {
            _logger.LogWarning("HandlePeerDownAsync called, but node {NodeId} not found in registry.", nodeId);
            return;
        }

        // Vet and remove via central peer manager
        bool removed = await _peerManager.TryRemovePeerAsync(node, async p =>
        {
            bool reachable = await CanReachNodeAsync(p);
            _logger.LogDebug("Vet before removal: node {NodeId} reachable? {Reachable}", p.NodeId, reachable);
            return !reachable;
        });

        if (removed)
        {
            RemoveConnection(nodeId);
        }
    }

}

