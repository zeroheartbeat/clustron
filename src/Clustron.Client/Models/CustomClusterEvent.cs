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
