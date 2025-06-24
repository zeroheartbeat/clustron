using Clustron.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Test
{
    public class TestEvent : ClusterEventBase<TestEvent>
    {
        public TestEvent()
        {
        }

        public int EventId { get; set; }
        public string SenderName { get; set; }
    }
}
