// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;

namespace Clustron.Core.Messaging;

public static class MessageBuilder
{
    private static IMessageSerializer? _serializer;

    public static void Configure(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public static Message Create<T>(string senderId, string type, string correlationId, T payload)
    {
        if (_serializer == null)
            throw new InvalidOperationException("MessageBuilder serializer not configured.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId must be provided.", nameof(correlationId));

        return new Message
        {
            MessageType = type,
            SenderId = senderId,
            CorrelationId = correlationId, 
            Payload = _serializer.Serialize(payload)
        };
    }

    public static Message LeaderChanged(NodeInfo leader, int epoch)
    {
        if (_serializer == null)
            throw new InvalidOperationException("MessageBuilder is not configured with a serializer.");

        return new Message
        {
            MessageType = MessageTypes.LeaderChanged,
            SenderId = leader.NodeId,
            Payload = _serializer.Serialize(leader)
        };
    }

    public static T DeserializePayload<T>(byte[] payload)
    {
        if (_serializer == null)
            throw new InvalidOperationException("MessageBuilder serializer not configured.");

        return _serializer.Deserialize<T>(payload);
    }
}
