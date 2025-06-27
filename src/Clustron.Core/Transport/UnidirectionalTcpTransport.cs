// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Discovery;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Clustron.Core.Transport;

public class UnidirectionalTcpTransport : BaseTcpTransport
{
    private readonly int _port;
    private readonly ClusterPeerManager _peerManager;
    private readonly IDiscoveryProvider _discoveryProvider;

    public UnidirectionalTcpTransport(
        int port,
        IClusterRuntime clusterRuntime,
        IMessageSerializer serializer,
        IClusterLoggerProvider loggerProvider)
        : base(clusterRuntime, serializer, loggerProvider.GetLogger<UnidirectionalTcpTransport>())
    {
        _port = port;
        _peerManager = clusterRuntime.PeerManager;
    }

    public override Task StartAsync(IMessageRouter router)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, router);
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
                    if (read == 0) break;

                    int length = BitConverter.ToInt32(lengthBuffer, 0);
                    if (length <= 0 || length > 1024 * 10) break;

                    var payloadBuffer = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        await ReadExactlyAsync(stream, payloadBuffer, 0, length);
                        var payload = payloadBuffer.AsSpan(0, length).ToArray();
                        var message = _serializer.Deserialize<Message>(payload);

                        if (message == null) continue;

                        senderId = message.SenderId;

                        if (!_peerManager.IsAlive(senderId))
                        {
                            var remoteEndpoint = (IPEndPoint?)client.Client.RemoteEndPoint;
                            if (remoteEndpoint != null)
                            {
                                var nodeInfo = new NodeInfo
                                {
                                    NodeId = senderId,
                                    Host = remoteEndpoint.Address.ToString(),
                                    Port = remoteEndpoint.Port
                                };

                                _peerManager.RegisterPeer(nodeInfo);
                                _logger.LogInformation("Registered new inbound peer: {SenderId}", senderId);
                            }
                        }

                        if (!string.IsNullOrEmpty(message.CorrelationId) &&
                            _responseAwaiters.TryGetValue(message.CorrelationId, out var tcs))
                        {
                            tcs.TrySetResult(message);
                            _responseAwaiters.TryRemove(message.CorrelationId, out _);
                        }
                        else
                        {
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
            _logger.LogWarning("Inbound connection failed: {Error}", ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(senderId))
            {
                await HandlePeerDownAsync(senderId); 
            }
            client.Close();
        }
    }

    public override async Task HandlePeerDownAsync(string nodeId)
    {
        var node = _peerManager.GetAllKnownPeers().FirstOrDefault(n => n.NodeId == nodeId);
        if (node == null)
        {
            _logger.LogWarning("HandlePeerDownAsync: node {NodeId} not found in known peers.", nodeId);
            return;
        }

        // Run vetting logic via centralized manager
        bool removed = await _peerManager.TryRemovePeerAsync(node, async p =>
        {
            bool reachable = await CanReachNodeAsync(p);
            _logger.LogDebug("Peer {NodeId} reachable? {Reachable}", p.NodeId, reachable);
            return !reachable;
        });

        if (removed)
        {
            RemoveConnection(nodeId);
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
            var conn = await GetOrCreateConnectionAsync(target);
            if (conn?.IsConnected != true)
            {
                _logger.LogWarning("Connection to {NodeId} unavailable.", target.NodeId);
                return;
            }

            conn.LastUsedUtc = DateTime.UtcNow;
            var stream = conn.Stream;

            await stream.WriteAsync(length, 0, length.Length);
            await stream.WriteAsync(body, 0, body.Length);
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

            _logger.LogInformation("Successfully connected to {NodeId}", target.NodeId);

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
            return conn?.IsConnected == true;
        }
        catch
        {
            return false;
        }
    }

    public override void RemoveConnection(string nodeId)
    {
        if (_connections.TryRemove(nodeId, out var conn))
        {
            conn.Dispose();
            _logger.LogInformation("Removed outbound connection to {NodeId}", nodeId);
        }
    }

    public override Task SendAsync(NodeInfo target, byte[] data)
    {
        throw new NotImplementedException();
    }

    public override Task SendImmediateAsync(NodeInfo target, Message message)
    {
        throw new NotImplementedException();
    }
}

