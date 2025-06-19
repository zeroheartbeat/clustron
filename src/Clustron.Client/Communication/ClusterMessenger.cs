// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Internals;
using Clustron.Client.Models;
using Clustron.Core.Client;
using Clustron.Core.Events;
using Clustron.Core.Extensions;
using Clustron.Core.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Communication
{
    public class ClusterMessenger : IMessenger
    {
        private readonly ClustronClientCore _core;
        private readonly Dictionary<string, Func<byte[], string, Task>> _typedDispatchers = new();


        public ClusterMessenger(ClustronClientCore core)
        {
            _core = core;
        }

        public async Task BroadcastAsync<T>(T data)
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");

            var payload = ClientMessageBuilder.Create<T>(data);
            var message = MessageBuilder.Create<ClientPayload<T>>(
            _core.Self.NodeId,                       // senderId
            ClientMessageTypes.ClientMessage,        // type
            Guid.NewGuid().ToString(),               // correlationId
            payload);

            await _core.BroadcastAsync(message);
        }

        public async Task SendAsync<T>(string nodeId, T data)
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");
            
            var payload = ClientMessageBuilder.Create<T>(data);

            var message = MessageBuilder.Create<ClientPayload<T>>(
            _core.Self.NodeId,                       // senderId
            ClientMessageTypes.ClientMessage,        // type
            Guid.NewGuid().ToString(),               // correlationId
            payload);

            await _core.SendAsync(message, nodeId);
        }

        public Task PublishAsync<T>(T @event) where T : IClusterEvent
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");
            
            return _core.PublishAsync(@event);
        }

        public void Subscribe<T>(Func<T, Task> handler) where T : IClusterEvent
        {
            _core.Subscribe(handler);
        }

        public void OnMessageReceived<T>(Func<T, string, Task> handler)
        {
            var messageType = ClientMessageTypes.ClientMessage;

            _typedDispatchers[messageType] = async (payloadBytes, senderId) =>
            {
                var clientPayload = _core.Serializer.Deserialize<ClientPayload<T>>(payloadBytes);
                await handler(clientPayload.Data, senderId);
            };
        }

        public bool TryGetHandler(string messageType, out Func<byte[], string, Task> dispatcher)
        {
            return _typedDispatchers.TryGetValue(messageType, out dispatcher);
        }
    }
}
