// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster.State;
using Clustron.Core.Events;
using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster
{
    public abstract class ClusterNodeControllerBase : IClusterNodeController, IClusterStateMutator
    {
        protected readonly NodeInfo Self;
        protected readonly JoinManager JoinManager;
        protected readonly ITransport Transport;
        protected readonly ILogger Logger;
        protected readonly ClusterPeerManager PeerManager;
        protected readonly IClusterCommunication ClusterCommunication;
        protected readonly IClusterRuntime ClusterRuntime;


        protected NodeInfo? _currentLeader;
        protected int _currentEpoch = 0;

        public virtual NodeInfo? CurrentLeader => _currentLeader;
        public IClusterCommunication Communication => ClusterCommunication;
        public IClusterRuntime Runtime => ClusterRuntime;
        public virtual int CurrentEpoch => _currentEpoch;

        protected ClusterNodeControllerBase(
            IClusterRuntime clusterRuntime,
            IClusterCommunication clusterCommunication,
            JoinManager joinManager,
            ILogger logger)
        {
            JoinManager = joinManager;
            PeerManager = clusterRuntime.PeerManager;
            Logger = logger;
            ClusterRuntime = clusterRuntime;

            Self = clusterRuntime.Self;
            ClusterCommunication = clusterCommunication;
        }

        public abstract Task StartAsync();

        public virtual async Task<bool> IsReachableAsync()
        {
            if (_currentLeader == null)
                return false;

            return await Transport.CanReachNodeAsync(_currentLeader);
        }

        public bool IsSelfLeader() => _currentLeader?.NodeId == Self.NodeId;

        public void SetLeader(NodeInfo leader, int epoch)
        {
            if (leader == null || string.IsNullOrWhiteSpace(leader.NodeId))
            {
                Logger.LogWarning("SetLeader called with null or invalid leader.");
                return;
            }

            if (CurrentLeader?.NodeId == leader.NodeId)
            {
                if (epoch < _currentEpoch)
                {
                    Logger.LogWarning("Received leader update for same leader {LeaderId} with lower epoch {Epoch} < {CurrentEpoch}", leader.NodeId, epoch, CurrentEpoch);
                    return;
                }

                if (epoch == _currentEpoch)
                {
                    return; // Already current
                }

                Logger.LogInformation("Updating epoch for existing leader {LeaderId} from {OldEpoch} to {NewEpoch}", leader.NodeId, CurrentEpoch, epoch);
                _currentEpoch = epoch;
                return;
            }

            if (epoch < _currentEpoch)
            {
                Logger.LogWarning("Ignoring stale SetLeader call from {LeaderId} with epoch {Epoch} < {CurrentEpoch}", leader.NodeId, epoch, CurrentEpoch);
                return;
            }

            var previous = CurrentLeader?.NodeId ?? "<none>";
            Logger.LogCritical("Changing leader from {OldLeader} to {NewLeader} with epoch {Epoch}", previous, leader.NodeId, epoch);

            _currentLeader = leader;
            _currentEpoch = epoch;

            // Publish LeaderChangedEvent
            ClusterRuntime.EventBus.Publish(new LeaderChangedEvent(leader, epoch));
        }

        public async Task HandleHeartbeatSuspectAsync(string suspectedNodeId, string reporterId)
        {
            Logger.LogWarning("Node {NodeId} suspected by {ReporterId}", suspectedNodeId, reporterId);
            if (!IsSelfLeader()) return;

            PeerManager.MarkPeerDown(new NodeInfo { NodeId = suspectedNodeId });
        }

        public void ForceLeader(NodeInfo leader, int epoch)
        {
            if (leader == null || string.IsNullOrWhiteSpace(leader.NodeId))
            {
                Logger.LogWarning("ForceLeader called with invalid leader.");
                return;
            }

            if (epoch < CurrentEpoch)
            {
                Logger.LogWarning("ForceLeader rejected: Epoch {Epoch} is older than current {CurrentEpoch}", epoch, CurrentEpoch);
                return;
            }

            if (leader.NodeId == Self.NodeId)
            {
                Logger.LogInformation("ForceLeader to self with epoch {Epoch}", epoch);
            }
            else if (CurrentLeader?.NodeId != leader.NodeId)
            {
                Logger.LogWarning("Forcefully switching leader from {OldLeader} to {NewLeader} at epoch {Epoch}",
                    CurrentLeader?.NodeId ?? "<none>", leader.NodeId, epoch);
            }

            SetLeader(leader, epoch);
        }

        public void RegisterAndAnnouncePeer(NodeInfo peer)
        {
            bool isNew = PeerManager.RegisterPeer(peer);
            if (isNew)
            {
                ClusterRuntime.EventBus.Publish(new NodeJoinedEvent(peer));
            }
        }

        public void MarkAndAnnouncePeerDown(NodeInfo peer)
        {
            bool wasAlive = PeerManager.MarkPeerDown(peer);
            if (wasAlive)
            {
                Logger.LogWarning("Node left: {NodeId}", peer.NodeId);
                ClusterRuntime.EventBus.Publish(new NodeLeftEvent(peer));
            }
        }
    }
}

