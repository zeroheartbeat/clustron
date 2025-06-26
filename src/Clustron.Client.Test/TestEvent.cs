// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
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
