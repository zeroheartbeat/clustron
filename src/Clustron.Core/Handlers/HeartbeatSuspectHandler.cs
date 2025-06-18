// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handlers
{
    public class HeartbeatSuspectHandler : IMessageHandler
    {
        private readonly ClusterNodeControllerBase _clusterLeader;
        private readonly NodeInfo _localNode;
        private readonly ILogger<HeartbeatSuspectHandler> _logger;
        private readonly IMessageSerializer _serializer;

        public HeartbeatSuspectHandler(
            ClusterNodeControllerBase clusterLeader,
            NodeInfo localNode, IMessageSerializer serializer,
            ILogger<HeartbeatSuspectHandler> logger)
        {
            _clusterLeader = clusterLeader;
            _localNode = localNode;
            _serializer = serializer;
            _logger = logger;
        }

        public string Type => MessageTypes.HeartbeatSuspect;

        public async Task HandleAsync(Message message)
        {
            var targetNodeId = _serializer.Deserialize<string>(message.Payload);
            _logger.LogWarning("Received HeartbeatSuspect for node: {NodeId} from {Sender}", targetNodeId, message.SenderId);

            if (_clusterLeader.CurrentLeader?.NodeId == _localNode.NodeId)
            {
                await _clusterLeader.HandleHeartbeatSuspectAsync(targetNodeId, message.SenderId);
            }
        }
    }
}

