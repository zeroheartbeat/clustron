using Clustron.Core.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;

public static class MessageClassifier
{
    public static MessagePriority GetPriority(string type) => type switch
    {
        MessageTypes.Heartbeat => MessagePriority.High,
        MessageTypes.LeaderChanged => MessagePriority.High,
        MessageTypes.MetricsRequest => MessagePriority.High,
        MessageTypes.ClientMessage => MessagePriority.Low,
        _ => MessagePriority.Medium
    };
}
