// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster;
using Clustron.Core.Cluster.State;
using Clustron.Core.Events;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Election
{
    public class LeaderElectionService : ILeaderElectionService
    {
        private readonly ElectionCoordinator _coordinator;
        private readonly ClusterPeerManager _peerManager;
        private readonly IClusterStateMutator _state;
        private readonly NodeInfo _self;
        private readonly ILogger<LeaderElectionService> _logger;
        private readonly IClusterCommunication _communicationProvider;

        private bool _electionInProgress = false;
        private DateTime _lastElectionTime = DateTime.MinValue;
        private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(3);

        public LeaderElectionService(
            IElectionCoordinatorProvider coordinatorProvider,
            IClusterRuntime clusterRuntime,
            IClusterStateMutator state,
            IClusterCommunication communicationProvider,
            IClusterLoggerProvider loggerProvider)
        {
            _coordinator = coordinatorProvider.Coordinator;
            _state = state;
            _self = clusterRuntime.Self;
            _peerManager = clusterRuntime.PeerManager;
            _communicationProvider = communicationProvider;
            _logger = loggerProvider.GetLogger<LeaderElectionService>();
        }

        public NodeInfo? CurrentLeader => _state.CurrentLeader;
        public int CurrentEpoch => _state.CurrentEpoch;

        public async Task TryHoldElectionAsync()
        {

            if (_electionInProgress)
            {
                _logger.LogInformation("Election already in progress.");
                return;
            }

            var now = DateTime.UtcNow;
            var timeSinceLast = now - _lastElectionTime;

            if (timeSinceLast < _cooldown)
            {
                var delay = _cooldown - timeSinceLast;
                _logger.LogInformation("Election cooldown active. Delaying by {Delay}ms", delay.TotalMilliseconds);
                await Task.Delay(delay);
            }

            _electionInProgress = true;
            _lastElectionTime = DateTime.UtcNow;

            try
            {
                var leader = await _coordinator.ElectLeaderAsync(_peerManager.GetActivePeers());
                if (leader != null)
                {
                    int newEpoch = CurrentEpoch + 1;
                    _state.SetLeader(leader, newEpoch);

                    var payload = new LeaderChangedEvent(leader, newEpoch);
                    var correlationId = Guid.NewGuid().ToString();
                    var message = MessageBuilder.Create<LeaderChangedEvent>(_self.NodeId, MessageTypes.LeaderChanged, correlationId, payload);
                    await _communicationProvider.Transport.BroadcastAsync(message);
                }
            }
            finally
            {
                _electionInProgress = false;
            }
        }
    }

}

