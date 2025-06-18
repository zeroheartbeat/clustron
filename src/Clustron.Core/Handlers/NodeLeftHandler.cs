// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Health;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handlers;
public class NodeLeftHandler : IMessageHandler
{
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<NodeLeftHandler> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly ClusterPeerManager _peerManager;

    public NodeLeftHandler(
        IHeartbeatMonitor heartbeatMonitor,
        IMessageSerializer serializer,
        ClusterPeerManager peerManager,
        ILogger<NodeLeftHandler> logger)
    {
        _heartbeatMonitor = heartbeatMonitor;
        _serializer = serializer;
        _peerManager = peerManager;
        _logger = logger;
    }

    public string Type => MessageTypes.NodeLeft;

    public Task HandleAsync(Message message)
    {
        var node = _serializer.Deserialize<NodeInfo>(message.Payload);

        if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
        {
            _logger.LogWarning("Received NodeLeft message with null or empty NodeInfo.");
            return Task.CompletedTask;
        }

        _logger.LogCritical("Node left: {NodeId}", node.NodeId);

        _heartbeatMonitor.MarkNodeLeft(node);
        _peerManager.MarkPeerDown(node); // lifecycle will be triggered from here

        return Task.CompletedTask;
    }
}

