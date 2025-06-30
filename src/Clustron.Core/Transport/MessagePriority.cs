using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;

public enum MessagePriority
{
    High,    // Heartbeats, elections
    Medium,  // Metrics, internal coordination
    Low      // Client traffic, logs
}
