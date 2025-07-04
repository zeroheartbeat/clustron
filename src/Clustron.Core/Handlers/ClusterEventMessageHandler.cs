// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Abstractions;
using Clustron.Core.Events;
using Clustron.Core.Messaging;
using Clustron.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Handlers
{
    public class ClusterEventMessageHandler : IMessageHandler
    {
        private readonly IClusterEventBus _eventBus;
        private readonly IMessageSerializer _serializer;
        private readonly string _localNodeId;

        public string Type => MessageTypes.CustomEvent;

        public ClusterEventMessageHandler(
            IClusterEventBus eventBus,
            IMessageSerializer serializer,
            string localNodeId)
        {
            _eventBus = eventBus;
            _localNodeId = localNodeId;
            _serializer = serializer;
        }

        public Task HandleAsync(Message message)
        {
            if (message.SenderId == _localNodeId)
                return Task.CompletedTask;

            return _eventBus.PublishFromNetworkAsync(message.Payload, message.CorrelationId);
        }
    }

}
