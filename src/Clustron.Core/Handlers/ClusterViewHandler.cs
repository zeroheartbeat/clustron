// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Handshake;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handlers;

public class ClusterViewHandler : IMessageHandler
{
    private readonly ILogger<ClusterViewHandler> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly IClusterRuntime _runtime;
    private readonly IHandshakeProtocol _handshake;

    public string Type => MessageTypes.ClusterView;

    public ClusterViewHandler(
        ILogger<ClusterViewHandler> logger,
        IMessageSerializer serializer,
        IClusterRuntime runtime,
        IHandshakeProtocol handshake)
    {
        _logger = logger;
        _serializer = serializer;
        _runtime = runtime;
        _handshake = handshake;
    }

    public async Task HandleAsync(Message message)
    {
        var payload = _serializer.Deserialize<ClusterViewPayload>(message.Payload);
        if (payload == null || payload.KnownPeers == null)
            return;

        _logger.LogCritical("KnownPeers received from {Sender} : {Peers}", message.SenderId, string.Join(", ", payload.KnownPeers.Select(p => $"{p.NodeId} (Roles: {string.Join("|", p.Roles)})")));

        foreach (var peer in payload.KnownPeers)
        {
            if (peer.NodeId == _runtime.Self.NodeId)
                continue;

            if (!_runtime.PeerManager.IsPeerKnown(peer.NodeId))
            {
                _logger.LogInformation("Discovered unknown peer {NodeId} via cluster view. Attempting handshake...", peer.NodeId);
                try
                {
                    await _handshake.InitiateHandshakeAsync(peer);
                    _logger.LogInformation("Successfully connected to {NodeId} via cluster view.", peer.NodeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Handshake with {NodeId} failed: {Error}", peer.NodeId, ex.Message);
                }
            }
        }
    }
}

