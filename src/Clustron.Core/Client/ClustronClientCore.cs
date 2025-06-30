// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io


using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Transport;
using Clustron.Core.Serialization;
using Clustron.Core.Cluster;
using Clustron.Core.Events;
using System.Collections.Concurrent;
using Clustron.Core.Configuration;
using Clustron.Abstractions;
using Clustron.Core.Observability;

namespace Clustron.Core.Client;

public class ClustronClientCore
{
    private readonly IMessageSerializer _serializer;
    private readonly NodeInfo _self;
    private readonly ClusterPeerManager _peerManager;
    private readonly IClusterEventBus _eventBus;
    private readonly IMetricContributor _metrics;
    private readonly ClusterNodeControllerBase _controller;

    private readonly ConcurrentDictionary<string, Func<byte[], string, Task>> _typedHandlers = new();

    public event Action<NodeInfo>? NodeJoined;
    public event Action<NodeInfo>? NodeLeft;
    public event Action<NodeInfo, int>? LeaderChanged;

    public ClustronClientCore(ClusterNodeControllerBase controller, IMetricContributor metrics, IMessageSerializer serializer)
    {
        _controller = controller;
        _serializer = serializer;
        _self = controller.Runtime.Self;
        _peerManager = controller.Runtime.PeerManager;
        _eventBus = controller.Runtime.EventBus;
        _metrics = metrics;
        
        _eventBus.Subscribe<NodeJoinedEvent>(e => NodeJoined?.Invoke(e.Node));
        _eventBus.Subscribe<NodeLeftEvent>(e => NodeLeft?.Invoke(e.Node));
        _eventBus.Subscribe<LeaderChangedEvent>(e => LeaderChanged?.Invoke(e.NewLeader, e.Epoch));
    }

    public IMessageSerializer Serializer => _serializer;
    public NodeInfo Self => _self;

    public async Task SendAsync(Message message, string targetNodeId)
    {
        await _controller.Communication.Transport.SendAsync(
            _peerManager.GetPeerById(targetNodeId), message);

        _metrics.Increment(MetricKeys.Msg.Direct.Sent);
    }

    public async Task BroadcastAsync(Message message, params string[] roles)
    {
        var recipients = _peerManager.GetActivePeers()
            .Where(p => roles.Length == 0 || p.Roles?.Intersect(roles, StringComparer.OrdinalIgnoreCase).Any() == true)
            .ToList();

        await _controller.Communication.Transport.BroadcastAsync(message, roles);

        _metrics.Increment(MetricKeys.Msg.Direct.Broadcasted);
    }

    public async Task PublishAsync<T>(T @event, EventDispatchOptions? options) where T : IClusterEvent
    {
        await _eventBus.PublishAsync(@event, options);

        _metrics.Increment(MetricKeys.Msg.Events.Published);
    }

    public void Subscribe<T>(Func<T, Task> handler) where T : IClusterEvent
    {
        _eventBus.Subscribe<T>(async evt =>
        {
            await handler(evt);

            // Track total delivered messages
            _metrics.Increment(MetricKeys.Msg.Events.Delivered);
        });
    }

    public void RegisterClientMessageHandler<T>(Func<T, string, Task> handler)
    {
        var messageType = MessageTypes.ClientMessage;

        _typedHandlers[messageType] = async (payloadBytes, senderId) =>
        {
            var deserialized = _serializer.Deserialize<T>(payloadBytes);
            await handler(deserialized, senderId);
            _metrics.Increment(MetricKeys.Msg.Direct.Received);
        };
    }


    public bool TryGetHandler(string messageType, out Func<byte[], string, Task> dispatcher)
    {
        return _typedHandlers.TryGetValue(messageType, out dispatcher);
    }

    public IEnumerable<NodeInfo> GetMembers() => _peerManager.GetPeersWithRole(ClustronRoles.Member);

    public IEnumerable<NodeInfo> GetMetricsCollectors() => _peerManager.GetPeersWithRole(ClustronRoles.MetricsCollector);
    public IEnumerable<NodeInfo> GetObservers() => _peerManager.GetPeersWithRole(ClustronRoles.Observer);

    public IEnumerable<NodeInfo> GetClients() => _peerManager.GetPeersWithRole(ClustronRoles.Client);

    public NodeInfo? GetCurrentLeader() => _controller.CurrentLeader;

    public IEnumerable<NodeInfo> GetPeersByRole(string role) =>
        _peerManager.GetActivePeers().Where(p => p.Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) == true);

    public IEnumerable<NodeInfo> GetPeersExceptRole(string role) =>
        _peerManager.GetActivePeers().Where(p => p.Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) == false);



    public bool IsPeerAlive(string nodeId) => _peerManager.IsAlive(nodeId);

}

