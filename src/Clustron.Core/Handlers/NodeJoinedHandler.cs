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

namespace Clustron.Core.Handlers;
public class NodeJoinedHandler : IMessageHandler
{
    private readonly ILogger<NodeJoinedHandler> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly ClusterPeerManager _peerManager;

    public NodeJoinedHandler(
        ILogger<NodeJoinedHandler> logger,
        IMessageSerializer serializer,
        ClusterPeerManager peerManager)
    {
        _logger = logger;
        _serializer = serializer;
        _peerManager = peerManager;
    }

    public string Type => MessageTypes.NodeJoined;

    public async Task HandleAsync(Message message)
    {
        var joinedEvent = _serializer.Deserialize<NodeJoinedEvent>(message.Payload);

        if (joinedEvent == null || string.IsNullOrEmpty(joinedEvent.Node.NodeId))
        {
            _logger.LogWarning("NodeJoinedHandler received invalid NodeInfo.");
            return;
        }

        // Register peer
        _peerManager.RegisterPeer(joinedEvent.Node);

        // OPTIONAL: If you plan to broadcast or notify later, leave the method async-ready
        await Task.CompletedTask;
    }
}

