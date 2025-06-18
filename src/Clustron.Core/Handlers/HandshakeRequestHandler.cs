// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Handshake;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Handlers;

public class HandshakeRequestHandler : IMessageHandler
{
    private readonly IHandshakeProtocol _protocol;
    private readonly IMessageSerializer _serializer;
    private readonly NodeInfo _self;
    private readonly ILogger<HandshakeRequestHandler> _logger;
    private readonly IClusterCommunication _communication;

    public HandshakeRequestHandler(
        IClusterRuntime clusterRuntime,
        IClusterCommunication communication,
        IHandshakeProtocol protocol,
        IMessageSerializer serializer, IClusterLoggerProvider loggerProvider)
    {
        _protocol = protocol;
        _serializer = serializer;
        _self = clusterRuntime.Self;
        _communication = communication; 
        _logger = loggerProvider.GetLogger< HandshakeRequestHandler>();
    }

    public string Type => MessageTypes.HandshakeRequest;

    public async Task HandleAsync(Message message)
    {
        var request = _serializer.Deserialize<HandshakeRequest>(message.Payload);
        if (request == null)
        {
            _logger.LogWarning("Received invalid handshake payload.");
            return;
        }

        var result = await _protocol.ProcessHandshake(request);

        var response = new HandshakeResponse
        {
            Accepted = result.Accepted,
            Reason = result.Reason,
            Leader = result.Leader,
            LeaderEpoch = result.LeaderEpoch,
            ResponderNode = _self 
        };

        var responseMessage = new Message
        {
            MessageType = MessageTypes.HandshakeResponse,
            SenderId = _self.NodeId,
            CorrelationId = message.CorrelationId,
            Payload = _serializer.Serialize(response)
        };

        await _communication.Transport.SendAsync(request.Sender, responseMessage);

        _logger.LogInformation("Sent handshake response to {PeerId}, CorrelationId={CorrelationId}",
            request.Sender?.NodeId ?? "<unknown>", message.CorrelationId);
    }

}

