// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Core.Events;
using Clustron.Core.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Models
{
    public class CustomClusterEvent<T> : ClusterEventBase<T>, ICustomClusterEvent
    {
        public CustomClusterEvent() { }

        public CustomClusterEvent(T payload)
        {
            EventType = MessageTypes.CustomEvent;
            Payload = payload;
        }

        object? ICustomClusterEvent.Payload => Payload;

        public string? Publisher { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

    }
}
