// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Abstractions;
using Clustron.Core.Messaging;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Events
{
    public class ClusterAwareEventPublisher
    {
        private readonly IClusterEventBus _eventBus;
        private readonly ITransport _transport;
        private readonly IMessageSerializer _serializer;
        private readonly string _nodeId;

        public ClusterAwareEventPublisher(
            IClusterEventBus eventBus,
            ITransport transport,
            IMessageSerializer serializer,
            string nodeId)
        {
            _eventBus = eventBus;
            _transport = transport;
            _serializer = serializer;
            _nodeId = nodeId;
        }

        //public async Task PublishAsync(IClusterEvent evt)
        //{
        //    // Local dispatch
        //    await _eventBus.PublishAsync(evt);

        //    // If this is a cluster-wide event, broadcast it
        //    if (evt is { Scope: ClusterEventScope.ClusterWide })
        //    {
        //        var message = new Message
        //        {
        //            MessageType = MessageTypes.CustomEvent,
        //            SenderId = _nodeId,
        //            CorrelationId = Guid.NewGuid().ToString(),
        //            Payload = SerializeWithKnownType(evt)
        //        };

        //        await _transport.BroadcastAsync(message);
        //    }
        //}

        //private byte[] SerializeWithKnownType(IClusterEvent evt)
        //{
        //    var type = evt.GetType();
        //    var method = typeof(IMessageSerializer).GetMethod("Serialize")!
        //        .MakeGenericMethod(type);

        //    return (byte[])method.Invoke(_serializer, new object[] { evt })!;
        //}
    }

}
