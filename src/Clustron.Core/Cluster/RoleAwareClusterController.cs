// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster.Behaviors;
using Clustron.Core.Events;
using Clustron.Core.Health;
using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster
{
    public class RoleAwareClusterController : ClusterNodeControllerBase
    {
        private readonly IEnumerable<IRoleAwareBehavior> _behaviors;
        private readonly IHeartbeatMonitor _heartbeatMonitor;
        private readonly ClusterPeerManager _peerManager;

        private readonly NodeInfo _self;

        public RoleAwareClusterController(
            IClusterRuntime clusterRuntime,
            JoinManager joinManager,
            IClusterCommunication communication,
            IHeartbeatMonitor heartbeatMonitor,
            IEnumerable<IRoleAwareBehavior> behaviors,
            IClusterLoggerProvider loggerProvider)
            : base(clusterRuntime, communication, joinManager, loggerProvider.GetLogger<RoleAwareClusterController>())
        {
            _behaviors = behaviors;
            _heartbeatMonitor = heartbeatMonitor;
            _self = clusterRuntime.Self;

            clusterRuntime.EventBus.Subscribe<NodeJoinedEvent>(e =>
            {
                PeerManager.RegisterPeer(e.Node);
            });

            clusterRuntime.EventBus.Subscribe<NodeLeftEvent>(e =>
            {
                Logger.LogWarning("Node left: {NodeId}", e.Node.NodeId);
            });

            clusterRuntime.EventBus.Subscribe<LeaderChangedEvent>(e =>
            {
                Logger.LogCritical("New leader: {NodeId} (Epoch {Epoch})", e.NewLeader.NodeId, e.Epoch);
            });
        }

        public override async Task StartAsync()
        {
            Logger.LogInformation("Joining cluster...");

            var result = await JoinManager.JoinClusterAsync(Self);

            foreach (var peer in result.Peers)
                PeerManager.RegisterPeer(peer);

            if (result.KnownLeader != null)
                SetLeader(result.KnownLeader, result.KnownLeaderEpoch);

            await Task.Delay(500); // allow handshakes to finish

            foreach (var behavior in _behaviors)
            {
                if (behavior is IRoleAwareBehavior roleAware && !roleAware.ShouldRunInRole(_self.Roles))
                {
                    Logger.LogInformation("Skipping behavior {Name} for roles: {Roles}", behavior.Name, string.Join(", ", _self.Roles));
                    continue;
                }

                Logger.LogInformation("Starting behavior: {RoleName}", behavior.Name);
                await behavior.StartAsync();
            }
        }
    }
}

