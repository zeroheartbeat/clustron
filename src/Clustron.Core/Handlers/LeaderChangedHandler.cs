// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Events;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Clustron.Core.Handlers
{
    public class LeaderChangedHandler : IMessageHandler
    {
        private readonly ClusterNodeControllerBase _clusterLeader;
        private readonly ILogger<LeaderChangedHandler> _logger;
        private readonly IMessageSerializer _serializer;

        public LeaderChangedHandler(ClusterNodeControllerBase clusterLeader, IMessageSerializer serializer, ILogger<LeaderChangedHandler> logger)
        {
            _clusterLeader = clusterLeader;
            _serializer = serializer;
            _logger = logger;
        }

        public string Type => MessageTypes.LeaderChanged;

        public async Task HandleAsync(Message message)
        {
            var payload = _serializer.Deserialize<LeaderChangedEvent>(message.Payload);

            if (payload?.NewLeader == null || string.IsNullOrWhiteSpace(payload.NewLeader.NodeId))
            {
                _logger.LogCritical($"Received LeaderChanged with null leader from {message.SenderId} — ignoring.");
                return;
            }

            var incomingLeader = payload.NewLeader;
            var incomingEpoch = payload.Epoch;
            var currentLeader = _clusterLeader.CurrentLeader;
            var currentEpoch = _clusterLeader.CurrentEpoch;

            if (incomingEpoch < currentEpoch)
            {
                _logger.LogWarning("Ignoring stale LeaderChanged: IncomingEpoch={IncomingEpoch} < CurrentEpoch={CurrentEpoch}",
                    incomingEpoch, currentEpoch);
                return;
            }

            if (incomingEpoch == currentEpoch)
            {
                if (currentLeader?.NodeId == incomingLeader.NodeId)
                {
                    _logger.LogDebug("Leader match and same epoch — ignoring.");
                    return;
                }

                // Tie-breaker
                var isCurrentReachable = await _clusterLeader.IsReachableAsync();

                if (!isCurrentReachable)
                {
                    _logger.LogWarning("Current leader unreachable — switching to incoming leader: {IncomingId}", incomingLeader.NodeId);
                    _clusterLeader.ForceLeader(incomingLeader, incomingEpoch);
                }
                else
                {
                    int cmp = string.CompareOrdinal(incomingLeader.NodeId, currentLeader.NodeId);
                    if (cmp < 0)
                    {
                        _logger.LogWarning("Incoming leader wins lexical tie-breaker — accepting: {IncomingId}", incomingLeader.NodeId);
                        _clusterLeader.ForceLeader(incomingLeader, incomingEpoch);
                    }
                    else
                    {
                        _logger.LogInformation("Current leader retained over {IncomingId} (tie-breaker).", incomingLeader.NodeId);
                    }
                }

                return;
            }

            // Incoming epoch is higher
            _logger.LogInformation("Newer leader received: {IncomingId} with epoch {Epoch}", incomingLeader.NodeId, incomingEpoch);
            _clusterLeader.ForceLeader(incomingLeader, incomingEpoch);
        }


    }
}

