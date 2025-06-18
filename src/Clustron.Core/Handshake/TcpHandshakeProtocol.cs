// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Cluster.State;
using Clustron.Core.Discovery;
using Clustron.Core.Handlers;
using Clustron.Core.Handshake;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handshake;

public class TcpHandshakeProtocol : IHandshakeProtocol
{
    private readonly NodeInfo _self;
    private readonly Lazy<IClusterState> _clusterState;
    private readonly IDiscoveryProvider _discoveryProvider;
    private readonly ILogger<TcpHandshakeProtocol> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly NodeJoinedHandler _nodeJoinedHandler;
    private readonly IClusterCommunication _communication;
    private readonly IClusterRuntime _runtime;

    public TcpHandshakeProtocol(
        IClusterRuntime clusterRuntime,
        IClusterCommunication communication,
        IClusterDiscovery discovery,
        IMessageSerializer serializer,
        Lazy<IClusterState> clusterState,
        NodeJoinedHandler nodeJoinedHandler,
        IClusterLoggerProvider loggerProvider)
    {
        _self = clusterRuntime.Self;
        _communication = communication;
        _discoveryProvider = discovery.DiscoveryProvider;
        _logger = loggerProvider.GetLogger<TcpHandshakeProtocol>();
        _clusterState = clusterState;
        _nodeJoinedHandler = nodeJoinedHandler;
        _serializer = serializer;
    }

    public async Task<HandshakeResponse> InitiateHandshakeAsync(NodeInfo targetNode)
    {
        try
        {
            var request = new HandshakeRequest
            {
                ClusterId = _self.ClusterId,
                Version = _self.Version,
                Sender = _self
            };

            var message = new Message
            {
                MessageType = MessageTypes.HandshakeRequest,
                SenderId = _self.NodeId,
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = _serializer.Serialize(request)
            };

            await _communication.Transport.SendAsync(targetNode, message);

            var rawResponse = await _communication.Transport.WaitForResponseAsync(
                targetNode.NodeId,
                message.CorrelationId,
                TimeSpan.FromSeconds(2));

            if (rawResponse == null)
            {
                _logger.LogWarning("No handshake response from {NodeId}", targetNode.NodeId);
                return new HandshakeResponse { Accepted = false, Reason = "No response", Leader = null };
            }

            var response = _serializer.Deserialize<HandshakeResponse>(rawResponse.Payload);

            if (response?.Leader != null)
            {
                _clusterState.Value.ForceLeader(response.Leader, response.LeaderEpoch);
            }

            return new HandshakeResponse
            {
                Accepted = response?.Accepted ?? false,
                Reason = response?.Reason ?? "Null response",
                Leader = response?.Leader,
                LeaderEpoch = response?.LeaderEpoch ?? 0,
                ResponderNode = response?.ResponderNode
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Handshake failed with {NodeId}: {Error}", targetNode.NodeId, ex.Message);
            return new HandshakeResponse { Accepted = false, Reason = $"Handshake exception: {ex.Message}" };
        }
    }

    public async Task<HandshakeResponse> ProcessHandshake(HandshakeRequest request)
    {
        var accepted = request.ClusterId == _self.ClusterId && request.Version == _self.Version;
        var reason = accepted ? "Accepted" : "Cluster/Version mismatch";

        if (accepted && _discoveryProvider is IRuntimeUpdatableDiscovery updater)
        {
            _logger.LogInformation("Registering discovered peer: {NodeId}", request.Sender.NodeId);
            await updater.RegisterPeerAsync(request.Sender);

            _logger.LogInformation("Locally triggering node join handler for {NodeId}", request.Sender.NodeId);

            var joinMessage = new Message
            {
                MessageType = MessageTypes.NodeJoined,
                SenderId = _self.NodeId,
                Payload = _serializer.Serialize(request.Sender)
            };

            await _nodeJoinedHandler.HandleAsync(joinMessage);
        }


        return new HandshakeResponse
        {
            Accepted = accepted,
            Reason = reason,
            Leader = _clusterState.Value.CurrentLeader,
            LeaderEpoch = _clusterState.Value.CurrentEpoch,
            ResponderNode = _self
        };
    }
}

