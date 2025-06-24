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

namespace Clustron.Core.Client;

public class ClustronClientCore
{
    private readonly IMessageSerializer _serializer;
    private readonly NodeInfo _self;
    private readonly ClusterPeerManager _peerManager;
    private readonly IClusterEventBus _eventBus;
    private readonly ClusterNodeControllerBase _controller;

    private readonly ConcurrentDictionary<string, Func<object, Task>> _handlers = new();

    public event Action<NodeInfo>? NodeJoined;
    public event Action<NodeInfo>? NodeLeft;
    public event Action<NodeInfo, int>? LeaderChanged;

    public ClustronClientCore(ClusterNodeControllerBase controller, IMessageSerializer serializer)
    {
        _controller = controller;
        _serializer = serializer;
        _self = controller.Runtime.Self;
        _peerManager = controller.Runtime.PeerManager;
        _eventBus = controller.Runtime.EventBus;
        
        _eventBus.Subscribe<NodeJoinedEvent>(e => NodeJoined?.Invoke(e.Node));
        _eventBus.Subscribe<NodeLeftEvent>(e => NodeLeft?.Invoke(e.Node));
        _eventBus.Subscribe<LeaderChangedEvent>(e => LeaderChanged?.Invoke(e.NewLeader, e.Epoch));
    }

    public IMessageSerializer Serializer => _serializer;
    public NodeInfo Self => _self;

    public Task SendAsync(Message message, string targetNodeId)
    {
        return _controller.Communication.Transport.SendAsync(_peerManager.GetPeerById(targetNodeId), message);
    }

    public Task BroadcastAsync(Message message, params string[] roles)
    {
        return _controller.Communication.Transport.BroadcastAsync(message, roles);
    }

    public Task PublishAsync<T>(T @event, EventDispatchOptions? options) where T : IClusterEvent
    {
        
        return _eventBus.PublishAsync(@event, options);
    }

    public void Subscribe<T>(Func<T, Task> handler) where T : IClusterEvent
    {
        _eventBus.Subscribe(handler);
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

