// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Configuration;
using Clustron.Core.Election;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster.Behaviors
{
    public class ElectionBehavior : IRoleAwareBehavior
    {
        private readonly ElectionCoordinator _coordinator;
        private readonly ILogger<ElectionBehavior> _logger;
        private readonly ClusterPeerManager _peerManager;

        public string Name => "Election";

        public ElectionBehavior(
            ElectionCoordinator coordinator,
            ClusterPeerManager peerManager,
            ILogger<ElectionBehavior> logger)
        {
            _coordinator = coordinator;
            _peerManager = peerManager;
            _logger = logger;
        }

        public Task StartAsync()
        {
            // Optional: leave empty or trigger election on startup from ElectionCoordinatorBehavior
            return Task.CompletedTask;
        }

        public async Task<NodeInfo?> RunElectionAsync()
        {
            var leader = await _coordinator.ElectLeaderAsync(_peerManager.GetActivePeers());

            if (leader != null)
            {
                _logger.LogInformation("Elected leader: {NodeId}", leader.NodeId);
            }
            else
            {
                _logger.LogWarning("No eligible leader could be elected.");
            }

            return leader;
        }

        public bool ShouldRunInRole(IList<string> roles)
            => roles.Contains(ClustronRoles.Member);
    }
}

