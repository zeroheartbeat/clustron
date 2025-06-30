using Clustron.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;

public class PrioritizedMessage
{
    public Message Message { get; init; }
    public MessagePriority Priority { get; init; }
    public string? TargetNodeId { get; init; }
    public string[]? Roles { get; init; }
}
