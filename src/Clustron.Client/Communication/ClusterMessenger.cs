// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Models;
using Clustron.Core.Client;
using Clustron.Core.Configuration;
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


        public ClusterMessenger(ClustronClientCore core)
        {
            _core = core;
        }

        public async Task BroadcastAsync<T>(T data)
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");

            var message = MessageBuilder.Create(
                _core.Self.NodeId,
                MessageTypes.ClientMessage,
                Guid.NewGuid().ToString(),
                data);

            await _core.BroadcastAsync(message, ClustronRoles.Member);
        }

        public async Task SendAsync<T>(string nodeId, T data)
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");

            var message = MessageBuilder.Create(
                _core.Self.NodeId,
                MessageTypes.ClientMessage,
                Guid.NewGuid().ToString(),
                data); // Send data directly

            await _core.SendAsync(message, nodeId);
        }

        public Task PublishAsync<T>(T @event, EventDispatchOptions? options = null)
                                            where T : IClusterEvent
        {
            if (!_core.Self.IsMember())
                throw new NotSupportedException("Only member nodes can send messages to other clients.");
            
            return _core.PublishAsync(@event, options);
        }

        public void Subscribe<T>(Func<T, Task> handler) where T : IClusterEvent
        {
            _core.Subscribe(handler);
        }

        public void Subscribe<TEvent, TPayload>(Func<TEvent, Task> handler)
                where TEvent : CustomClusterEvent<TPayload>, new()
        {
            _core.Subscribe<TEvent>(handler);
        }

        public void OnMessageReceived<T>(Func<T, string, Task> handler)
        {
            _core.RegisterClientMessageHandler<T>(handler);
        }

        
    }
}
