    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace Clustron.Client.Models
    {
        public interface ICustomClusterEvent
        {
            string EventType { get; }
            object? Payload { get; }
            string? Publisher { get; set; }
        }
    }
