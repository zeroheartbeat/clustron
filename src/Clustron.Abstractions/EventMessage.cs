using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Abstractions
{
    public class EventMessage : Message
    {
        public string EventTypeName { get; set; }
    }
}
