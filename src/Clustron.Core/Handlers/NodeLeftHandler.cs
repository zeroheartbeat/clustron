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
using Clustron.Core.Health;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Clustron.Core.Handlers
{
    public class NodeLeftHandler : IMessageHandler
    {
        private readonly ILogger<NodeLeftHandler> _logger;
        private readonly IMessageSerializer _serializer;
        private readonly ClusterPeerManager _peerManager;
        private readonly IHeartbeatMonitor _heartbeatMonitor;

        public NodeLeftHandler(
            IHeartbeatMonitor heartbeatMonitor,
            IMessageSerializer serializer,
            ClusterPeerManager peerManager,
            ILogger<NodeLeftHandler> logger)
        {
            _serializer = serializer;
            _peerManager = peerManager;
            _logger = logger;
            _heartbeatMonitor = heartbeatMonitor;
        }

        public string Type => MessageTypes.NodeLeft;

        public async Task HandleAsync(Message message)
        {
            var nodeLeftEvent = _serializer.Deserialize<NodeLeftEvent>(message.Payload);

            if (nodeLeftEvent == null || string.IsNullOrWhiteSpace(nodeLeftEvent.Node.NodeId))
            {
                _logger.LogWarning("Received NodeLeft message with null or empty NodeInfo.");
                return;
            }

            var target = nodeLeftEvent.Node;

            _logger.LogWarning("Received NodeLeft notice for {NodeId} from sender {SenderId}", target.NodeId, message.SenderId);

            await _heartbeatMonitor.MarkNodeLeft(target);
        }
    }
}
