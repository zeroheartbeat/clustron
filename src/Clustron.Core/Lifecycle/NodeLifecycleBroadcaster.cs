// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using System.Collections.Concurrent;

public class NodeLifecycleBroadcaster
{
    private readonly List<INodeLifecycleListener> _listeners = new();
    private readonly ConcurrentDictionary<string, bool> _activeNodes = new();

    public void AddListener(INodeLifecycleListener listener)
    {
        if (!_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public IEnumerable<INodeLifecycleListener> Listeners => _listeners;

    public async Task NotifyNodeJoinedAsync(NodeInfo node)
    {
        // Only notify if node isn't currently marked as active
        if (_activeNodes.ContainsKey(node.NodeId))
            return;

        _activeNodes[node.NodeId] = true;

        foreach (var listener in _listeners)
            await listener.OnNodeJoinedAsync(node);
    }

    public async Task NotifyNodeLeftAsync(NodeInfo node)
    {
        // Only notify if node was active
        if (!_activeNodes.TryRemove(node.NodeId, out _))
            return;

        foreach (var listener in _listeners)
            await listener.OnNodeLeftAsync(node);
    }

    public bool IsNodeActive(string nodeId) => _activeNodes.ContainsKey(nodeId);
}

