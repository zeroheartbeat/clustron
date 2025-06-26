// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Core.Events;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster
{
    public class ClusterPeerManager
    {
        private readonly PeerRegistry _registry;
        private readonly IClusterEventBus _eventBus;
        private readonly ILogger<ClusterPeerManager> _logger;

        private NodeInfo? _currentLeader;
        private int _currentEpoch;

        public NodeInfo Self => _registry.Self;

        public ClusterPeerManager(PeerRegistry registry, IClusterEventBus eventBus, ILogger<ClusterPeerManager> logger)
        {
            _registry = registry;
            _eventBus = eventBus;
            _logger = logger;
        }

        // --- PEER ADDITION ---
        public bool RegisterPeer(NodeInfo peer)
        {
            if (peer.NodeId == Self.NodeId)
                return false;

            bool isNew = _registry.RegisterPeer(peer);
            if (isNew)
            {
                _logger.LogInformation("Node joined: {NodeId}", peer.NodeId);
                _eventBus.Publish(new NodeJoinedEvent(peer));
            }

            return isNew;
        }

        // --- PEER REMOVAL (With Optional Vetting) ---
        public async Task<bool> TryRemovePeerAsync(NodeInfo peer, Func<NodeInfo, Task<bool>>? vettingCallback = null)
        {
            if (!_registry.IsAlive(peer.NodeId))
            {
                _logger.LogDebug("Peer {NodeId} not found in active peers", peer.NodeId);
                return false;
            }

            if (vettingCallback != null)
            {
                bool permitted = await vettingCallback(peer);
                if (!permitted)
                {
                    _logger.LogInformation("Removal vetoed for peer {NodeId}", peer.NodeId);
                    return false;
                }
            }

            bool wasAlive = _registry.MarkPeerDown(peer);
            if (wasAlive)
            {
                _logger.LogWarning("Node left: {NodeId}", peer.NodeId);
                _eventBus.Publish(new NodeLeftEvent(peer));
            }

            return wasAlive;
        }

        // --- LEADER MANAGEMENT ---
        public bool TrySetLeader(NodeInfo leader, int epoch)
        {
            if (leader == null || string.IsNullOrWhiteSpace(leader.NodeId))
            {
                _logger.LogWarning("TrySetLeader called with null or invalid leader.");
                return false;
            }

            if (_currentLeader?.NodeId == leader.NodeId)
            {
                if (epoch < _currentEpoch)
                {
                    _logger.LogWarning("Ignoring leader update for {LeaderId} with older epoch {Epoch}", leader.NodeId, epoch);
                    return false;
                }

                if (epoch == _currentEpoch)
                    return false;

                _logger.LogInformation("Updating epoch for leader {LeaderId}: {OldEpoch} ? {NewEpoch}", leader.NodeId, _currentEpoch, epoch);
                _currentEpoch = epoch;
                return true;
            }

            if (epoch < _currentEpoch)
            {
                _logger.LogWarning("Stale leader claim ignored from {LeaderId} (epoch {Epoch} < {CurrentEpoch})", leader.NodeId, epoch, _currentEpoch);
                return false;
            }

            string prev = _currentLeader?.NodeId ?? "<none>";
            _logger.LogCritical("Changing leader from {OldLeader} to {NewLeader} (epoch {Epoch})", prev, leader.NodeId, epoch);

            _currentLeader = leader;
            _currentEpoch = epoch;

            _eventBus.Publish(new LeaderChangedEvent(leader, epoch));
            return true;
        }

        public NodeInfo? GetLeader() => _currentLeader;
        public int GetCurrentEpoch() => _currentEpoch;

        // --- LIVENESS TRACKING ---
        public void MarkHeartbeatReceived(string nodeId) => _registry.MarkHeartbeatReceived(nodeId);
        public void RecordMissedHeartbeat(string nodeId) => _registry.RecordMissedHeartbeat(nodeId);
        public int GetMissCount(string nodeId) => _registry.GetMissCount(nodeId);
        public bool HasTimedOut(string nodeId, TimeSpan timeout) => _registry.HasTimedOut(nodeId, timeout);
        public bool IsAlive(string nodeId) => _registry.IsAlive(nodeId);

        // --- ACCESSORS ---
        public NodeInfo GetPeerById(string id) => _registry.GetPeer(id);
        public IEnumerable<NodeInfo> GetActivePeers() => _registry.GetActivePeers();
        public IEnumerable<NodeInfo> GetAllKnownPeers() => _registry.GetAllKnownPeers();
        public IEnumerable<NodeInfo> GetPeersWithRole(params string[] roles) => _registry.GetPeersWithRole(roles);
        public bool IsPeerKnown(string nodeId) => _registry.Contains(nodeId);

        public PeerRegistry Registry => _registry; // Consider making this internal-only
    }
}
