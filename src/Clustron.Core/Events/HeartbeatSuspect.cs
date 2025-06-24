using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Events
{
    public class HeartbeatSuspect
    {
        public NodeInfo SuspectedNode { get; init; }
        public HeartbeatSuspect(NodeInfo suspectedNode) 
        {
            SuspectedNode = suspectedNode;
        }
    }
}
