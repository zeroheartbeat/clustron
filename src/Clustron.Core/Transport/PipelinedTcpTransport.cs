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
using Clustron.Core.Helpers;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Clustron.Core.Transport;

public class PipelinedTcpTransport : BaseTcpTransport
{
    private readonly int _port;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, Channel<byte[]>> _outgoingChannels = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _teardownLocks = new();

    private readonly IMetricContributor _metrics;
    private readonly RetryOptions _retryOptions;

    public PipelinedTcpTransport(
        int port,
        IClusterRuntime runtime,
        IMessageSerializer serializer,
        IMetricContributor metrics,
        IClusterLoggerProvider loggerProvider,
        RetryOptions retryOptions)
        : base(runtime, serializer, loggerProvider.GetLogger<PipelinedTcpTransport>())
    {
        _port = port;
        _metrics = metrics;
        _retryOptions = retryOptions;
    }

    public override Task StartAsync(IMessageRouter router)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, router);
            }
        });

        return Task.CompletedTask;
    }

    private async Task HandleClientAsync(TcpClient client, IMessageRouter router)
    {
        using var stream = client.GetStream();
        var reader = PipeReader.Create(stream);
        string? senderId = null;

        try
        {
            int readCount = 0;

            while (true)
            {
                var result = await reader.ReadAsync();

                var buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out var message))
                {
                    senderId = message.SenderId;

                    if (!string.IsNullOrEmpty(message.CorrelationId) &&
                        _responseAwaiters.TryRemove(message.CorrelationId, out var tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                    else
                    {
                        await router.RouteAsync(message);
                        _metrics.Increment(MetricKeys.Msg.Wire.Received);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    if (!string.IsNullOrEmpty(senderId))
                    {
                        _logger.LogInformation("Client {SenderId} disconnected gracefully", senderId);
                        await HandlePeerDownAsync(senderId); 
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("HandleClientAsync failed: {Error}", ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(senderId))
                await HandlePeerDownAsync(senderId);

            client.Close();
        }
    }

    public virtual async Task HandlePeerDownAsync(string nodeId)
    {
        var teardownLock = _teardownLocks.GetOrAdd(nodeId, _ => new SemaphoreSlim(1, 1));
        await teardownLock.WaitAsync();

        try
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

            if(removed)
                RemoveConnection(nodeId);
        }
        finally
        {
            
            _teardownLocks.TryRemove(nodeId, out _); 
            teardownLock.Release();
        }
    }

    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out Message? message)
    {
        message = null;
        var reader = new SequenceReader<byte>(buffer);

        if (!reader.TryReadLittleEndian(out int length) || reader.Remaining < length)
            return false;

        var payload = buffer.Slice(reader.Position, length);
        message = _serializer.Deserialize<Message>(payload.ToArray());

        buffer = buffer.Slice(reader.Position).Slice(length);
        return true;
    }

    public override async Task SendAsync(NodeInfo target, Message message)
    {
        var data = _serializer.Serialize(message);
        await SendAsync(target, data);
    }

    public override async Task SendAsync(NodeInfo target, byte[] data)
    {
        var channel = GetOrCreateValidChannel(target);

        if (!channel.Writer.TryWrite(data))
        {
            try
            {
                await channel.Writer.WriteAsync(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WriteAsync failed to {NodeId}", target.NodeId);
                _outgoingChannels.TryRemove(target.NodeId, out _);
            }
        }
    }

    private Channel<byte[]> GetOrCreateValidChannel(NodeInfo target)
    {
        
        while (true)
        {
            if (_outgoingChannels.TryGetValue(target.NodeId, out var existing))
            {
                if (!existing.Reader.Completion.IsCompleted)
                    return existing;

                _outgoingChannels.TryRemove(target.NodeId, out _);
            }

            var newChannel = CreateChannel(target);
            if (_outgoingChannels.TryAdd(target.NodeId, newChannel))
                return newChannel;
        }
    }

    private Channel<byte[]> CreateChannel(NodeInfo target)
    {
        _logger.LogCritical("Creating new channel for {NodeId}", target.NodeId);

        if (!_peerManager.IsAlive(target.NodeId))
        {
            _logger.LogWarning("Refusing to create channel for dead node {NodeId}", target.NodeId);
            throw new IOException($"Node {target.NodeId} is marked down");
        }

        var ch = Channel.CreateUnbounded<byte[]>();
        _ = Task.Run(() => ProcessSendQueue(target, ch.Reader));
        return ch;
    }

    private async Task ProcessSendQueue(NodeInfo target, ChannelReader<byte[]> reader)
    {
        PersistentConnection? conn = null;
        var lockObj = _sendLocks.GetOrAdd(target.NodeId, _ => new SemaphoreSlim(1, 1));

        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var data))
            {
                await lockObj.WaitAsync();

                try
                {
                    await RetryHelper.RetryAsync(async () =>
                    {
                        conn ??= await GetOrCreateConnectionAsync(target);
                        if (conn?.IsConnected != true)
                        {
                            conn?.Dispose();
                            _connections.TryRemove(target.NodeId, out _);
                            conn = await GetOrCreateConnectionAsync(target);
                        }

                        if (conn == null || !conn.IsConnected)
                            throw new IOException("No available connection");

                        var stream = conn.Stream;
                        var buffer = ArrayPool<byte>.Shared.Rent(4 + data.Length);
                        try
                        {
                            BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), data.Length);
                            Buffer.BlockCopy(data, 0, buffer, 4, data.Length);
                            await stream.WriteAsync(buffer.AsMemory(0, 4 + data.Length));
                            conn.LastUsedUtc = DateTime.UtcNow;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }, _retryOptions.MaxAttempts, _retryOptions.DelayMilliseconds, _logger);

                    _metrics.Increment(MetricKeys.Msg.Wire.Sent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Send failed to {NodeId}: {Message}", target.NodeId, ex.Message);

                    conn?.Dispose();
                    _connections.TryRemove(target.NodeId, out _);
                    conn = null;

                    await HandlePeerDownAsync(target.NodeId);

                    // Mark the channel complete to exit the send loop
                    if (_outgoingChannels.TryRemove(target.NodeId, out var channel))
                    {
                        channel.Writer.TryComplete();
                    }

                    return; // Break out of loop — no point retrying further
                }

                finally
                {
                    lockObj.Release();
                }
            }
        }
    }

    private async Task<PersistentConnection?> GetOrCreateConnectionAsync(NodeInfo target)
    {
        if (_teardownLocks.TryGetValue(target.NodeId, out var teardownLock))
            await teardownLock.WaitAsync(); // Wait until the node is fully processed
        try
        {
            if (_connections.TryGetValue(target.NodeId, out var existingConn) && existingConn.IsConnected)
            {
                existingConn.LastUsedUtc = DateTime.UtcNow;
                return existingConn;
            }

            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(target.Host, target.Port);
                var conn = new PersistentConnection(client)
                {
                    RemoteNodeId = target.NodeId,
                    IsInbound = false
                };
                _connections[target.NodeId] = conn;
                return conn;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to {NodeId}: {Error}", target.NodeId, ex.Message);
                return null;
            }
        }
        finally
        {
            teardownLock?.Release();
        }
    }

    public async override Task SendImmediateAsync(NodeInfo target, Message message)
    {
        var data = _serializer.Serialize(message);
        await SendImmediateAsync(target, data);
    }

    public async override Task SendImmediateAsync(NodeInfo target, byte[] data)
    {
        var lockObj = _sendLocks.GetOrAdd(target.NodeId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            var conn = await GetOrCreateConnectionAsync(target);
            if (conn?.IsConnected != true)
                throw new IOException("Connection unavailable");

            var stream = conn.Stream;
            var buffer = ArrayPool<byte>.Shared.Rent(4 + data.Length);
            try
            {
                BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), data.Length);
                Buffer.BlockCopy(data, 0, buffer, 4, data.Length);

                await stream.WriteAsync(buffer.AsMemory(0, 4 + data.Length));
                conn.LastUsedUtc = DateTime.UtcNow;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            _metrics.Increment(MetricKeys.Msg.Wire.Sent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Immediate send failed to {NodeId}: {Message}", target.NodeId, ex.Message);
            _connections.TryRemove(target.NodeId, out var failedConn);
            failedConn?.Dispose();
            throw;
        }
        finally
        {
            lockObj.Release();
        }
    }


    public override Task<bool> CanReachNodeAsync(NodeInfo node)
    {
        return Task.FromResult(_connections.TryGetValue(node.NodeId, out var conn) && conn.IsConnected);
    }

    public override void RemoveConnection(string nodeId)
    {
        if (_connections.TryRemove(nodeId, out var conn))
        {
            conn.Dispose();
            _logger.LogInformation("Removed connection to {NodeId}", nodeId);
        }

        if (_outgoingChannels.TryRemove(nodeId, out var channel))
        {
            channel.Writer.TryComplete(); // Signal send loop to exit
            _logger.LogInformation("Closed send channel for {NodeId}", nodeId);
        }
        _sendLocks.TryRemove(nodeId, out _);
    }
}
