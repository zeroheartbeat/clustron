// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Events;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster
{
    public class ClusterPeerManager
    {
        private readonly PeerRegistry _registry;
        private readonly IClusterEventBus _eventBus;
        private readonly ILogger<ClusterPeerManager> _logger;

        public NodeInfo Self { get { return _registry.Self; } }

        public ClusterPeerManager(PeerRegistry registry, IClusterEventBus eventBus, ILogger<ClusterPeerManager> logger)
        {
            _registry = registry;
            _eventBus = eventBus;
            _logger = logger;
        }

        public bool RegisterPeer(NodeInfo peer)
        {
            _logger.LogInformation("Registering peer {NodeId} with roles: {Roles}", peer.NodeId, string.Join(",", peer.Roles));

            bool isNew = _registry.RegisterPeer(peer);
            if (isNew)
            {
                _logger.LogInformation("Node joined: {NodeId}", peer.NodeId);
                _eventBus.Publish(new NodeJoinedEvent(peer));
            }

            return isNew;
        }

        public void MarkHeartbeatReceived(string nodeId)
        { 
            _registry.MarkHeartbeatReceived(nodeId);
        }

        public void RecordMissedHeartbeat(string nodeId)
        { 
            _registry.RecordMissedHeartbeat(nodeId);
        }

        public int GetMissCount(string nodeId) 
        {
            return _registry.GetMissCount(nodeId);
        }

        public bool HasTimedOut(string nodeId, TimeSpan timeout)
        { 
            return _registry.HasTimedOut(nodeId, timeout);
        }

        public bool IsAlive(string nodeId)
        { 
            return _registry.IsAlive(nodeId);
        }

        public IEnumerable<NodeInfo> GetAllKnownPeers()
        {
            return _registry.GetAllKnownPeers();
        }

        public bool MarkPeerDown(NodeInfo peer)
        {
            bool wasAlive = _registry.MarkPeerDown(peer);
            if (wasAlive)
            {
                _logger.LogWarning("Node left: {NodeId}", peer.NodeId);
                _eventBus.Publish(new NodeLeftEvent(peer));
            }

            return wasAlive;
        }

        public PeerRegistry Registry => _registry;

        public NodeInfo GetPeerById(string id)
        {
            return _registry.GetPeer(id);
        }

        public IEnumerable<NodeInfo> GetPeersWithRole(string role)
        {
            return _registry.GetPeersWithRole(role);
        }
        public IEnumerable<NodeInfo> GetActivePeers()
        {
            return _registry.GetActivePeers();
        }

        public bool IsPeerKnown(string nodeId)
        {
            return _registry.Contains(nodeId);
        }
    }
}

