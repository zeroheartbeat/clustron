// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Discovery;
using Clustron.Core.Events;
using Clustron.Core.Extensions;
using Clustron.Core.Models;
using System.Collections.Concurrent;

namespace Clustron.Core.Cluster;

public class PeerRegistry
{
    private readonly NodeInfo _self;
    private readonly IDiscoveryProvider _discoveryProvider;
    private readonly ConcurrentDictionary<string, NodeInfo> _knownPeers = new();
    private readonly ConcurrentDictionary<string, NodeInfo> _activePeers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
    private readonly ConcurrentDictionary<string, int> _missCounts = new();

    public NodeInfo Self => _self;
    public PeerRegistry(NodeInfo self, IDiscoveryProvider discoveryProvider)
    {
        _self = self;   
        _discoveryProvider = discoveryProvider;
    }

    public IEnumerable<NodeInfo> GetActivePeers()
    {
        return _activePeers.Values;
    }

    public IEnumerable<NodeInfo> GetAllKnownPeers()
    {
        return _knownPeers.Values;
    }

    public bool IsAlive(string nodeId)
    {
        return nodeId != _self.NodeId && _activePeers.ContainsKey(nodeId);
    }

    public bool RegisterPeer(NodeInfo peer)
    {
        if (peer.NodeId == _self.NodeId) return false;

        bool isNew = !_activePeers.ContainsKey(peer.NodeId);
        _knownPeers[peer.NodeId] = peer;
        _activePeers[peer.NodeId] = peer;
        _lastSeen[peer.NodeId] = DateTime.UtcNow;
        _missCounts[peer.NodeId] = 0;

        if(_discoveryProvider is IRuntimeUpdatableDiscovery updater)
            updater.RegisterPeerAsync(peer);

        return isNew;
    }

    public bool MarkPeerDown(NodeInfo peer)
    {
        if (!_activePeers.ContainsKey(peer.NodeId))
            return false;

        ClearLivenessTracking(peer.NodeId);
        return true;
    }

    public NodeInfo GetPeer(string id) =>
                _activePeers.Values.Where(p => p.NodeId == id).FirstOrDefault();

    public IEnumerable<NodeInfo> GetPeersWithRole(params string[] roles) =>
                roles.Length == 0
                    ? _activePeers.Values
                    : _activePeers.Values.Where(p => roles.Any(r => p.HasRole(r)));

    public IEnumerable<NodeInfo> GetPeersWithAnyRole(params string[] roles)
    {
        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        return _activePeers.Values.Where(p =>
            p.Roles != null && p.Roles.Any(role => roleSet.Contains(role)));
    }

    public void MarkHeartbeatReceived(string nodeId)
    {
        _lastSeen[nodeId] = DateTime.UtcNow;
        _missCounts[nodeId] = 0;
    }

    public void RecordMissedHeartbeat(string nodeId)
    {
        _missCounts.AddOrUpdate(nodeId, 1, (_, val) => val + 1);
    }

    public bool HasTimedOut(string nodeId, TimeSpan timeout)
    {
        return _lastSeen.TryGetValue(nodeId, out var last) && (DateTime.UtcNow - last) > timeout;
    }

    public int GetMissCount(string nodeId) =>
        _missCounts.TryGetValue(nodeId, out var val) ? val : 0;

    public void ClearLivenessTracking(string nodeId)
    {
        _activePeers.TryRemove(nodeId, out _);
        _knownPeers.TryRemove(nodeId, out _);
        _lastSeen.TryRemove(nodeId, out _);
        _missCounts.TryRemove(nodeId, out _);
    }

    public bool Contains(string nodeId) => _knownPeers.ContainsKey(nodeId);
}

