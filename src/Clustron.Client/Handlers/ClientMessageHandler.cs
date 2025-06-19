// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Client;
using Clustron.Client.Models;
using Clustron.Core.Messaging;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Clustron.Client.Handlers;

public class ClientMessageHandler : IMessageHandler
{
    public string Type => ClientMessageTypes.ClientMessage;

    private readonly IClustronClient _client;
    private readonly ILogger<ClientMessageHandler> _logger;

    public ClientMessageHandler(
        IClustronClient client,
        ILogger<ClientMessageHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task HandleAsync(Message message)
    {
        if (!_client.Messaging.TryGetHandler(message.MessageType, out var dispatcher))
        {
            _logger.LogWarning("No handler registered for message type: {Type}", message.MessageType);
            return;
        }

        try
        {
            await dispatcher(message.Payload, message.SenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching message: {Type}", message.MessageType);
        }
    }
}

