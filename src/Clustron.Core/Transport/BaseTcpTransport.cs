// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Clustron.Core.Transport;

public abstract class BaseTcpTransport : ITransport
{
    protected readonly ILogger _logger;
    protected readonly IMessageSerializer _serializer;
    protected readonly ClusterPeerManager _peerManager;
    protected readonly ConcurrentDictionary<string, PersistentConnection> _connections = new();

    protected readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _responseAwaiters = new();
    protected readonly ConcurrentDictionary<string, SemaphoreSlim> _sendLocks = new();
    Timer _timer;

    protected BaseTcpTransport(
        IClusterRuntime clusterRuntime,
        IMessageSerializer serializer, ILogger logger)
    {
        _peerManager = clusterRuntime.PeerManager;
        _serializer = serializer;

        _logger = logger;

        _timer = new Timer(_ => CleanupIdleConnections(TimeSpan.FromMinutes(1)), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public abstract Task StartAsync(IMessageRouter router);
    public abstract Task SendAsync(NodeInfo target, Message message);

    public abstract Task SendAsync(NodeInfo target, byte[] data);
    public abstract void RemoveConnection(string nodeId);
    public abstract Task<bool> CanReachNodeAsync(NodeInfo node);

    public virtual async Task BroadcastAsync(Message message, params string[] roles)
    {
        var peers = _peerManager.GetPeersWithRole(roles);
        var body = _serializer.Serialize(message);

        var tasks = new List<Task>();

        foreach (var peer in peers)
        {
            if (peer.NodeId == message.SenderId)
                continue;

            tasks.Add(SendSafeAsync(peer, body));
        }

        await Task.WhenAll(tasks);
    }

    public virtual Task<Message> WaitForResponseAsync(string expectedSenderId, string correlationId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        _responseAwaiters[correlationId] = tcs;

        var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() =>
        {
            if (_responseAwaiters.TryRemove(correlationId, out var existing))
                existing.TrySetCanceled();
        });

        return tcs.Task;
    }

    public virtual Task HandlePeerDownAsync(string nodeId)
    {
        RemoveConnection(nodeId);
        return Task.CompletedTask;
    }

    protected static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int readTotal = 0;
        while (readTotal < count)
        {
            int read = await stream.ReadAsync(buffer, offset + readTotal, count - readTotal);
            if (read == 0)
                throw new EndOfStreamException($"Expected {count} bytes but got {readTotal}");
            readTotal += read;
        }
    }

    private async Task SendSafeAsync(NodeInfo peer, byte[] data)
    {
        try
        {
            await SendAsync(peer, data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send broadcast to {NodeId}: {Message}", peer.NodeId, ex.Message);
        }
    }

    private void CleanupIdleConnections(TimeSpan idleTimeout)
    {
        foreach (var conn in _connections)
        {
            if (DateTime.UtcNow - conn.Value.LastUsedUtc > idleTimeout)
            {
                _logger.LogWarning(
                "Disposing idle connection to {RemoteNodeId} (idle for {IdleDuration} seconds)",
                conn.Value.RemoteNodeId, (int)idleTimeout.TotalSeconds);

                HandlePeerDownAsync(conn.Value.RemoteNodeId);
            }
        }
    }

    public abstract Task SendImmediateAsync(NodeInfo target, Message message);
}

