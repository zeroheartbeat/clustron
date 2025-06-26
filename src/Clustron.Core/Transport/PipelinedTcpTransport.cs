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
    private readonly ILogger _logger;
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

                if (result.IsCompleted) break;
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
        var channel = _outgoingChannels.GetOrAdd(target.NodeId, key =>
        {
            var ch = Channel.CreateUnbounded<byte[]>();
            _ = Task.Run(() => ProcessSendQueue(target, ch.Reader));
            return ch;
        });

        // Fast path
        if (!channel.Writer.TryWrite(data))
            await channel.Writer.WriteAsync(data);
    }

    private async Task ProcessSendQueue(NodeInfo target, ChannelReader<byte[]> reader)
    {
        PersistentConnection? conn = null;
        while (await reader.WaitToReadAsync())
        {
            while (reader.TryRead(out var data))
            {
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
                    await Task.Delay(100);
                }
            }
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

        _outgoingChannels.TryRemove(nodeId, out _);
    }
}
