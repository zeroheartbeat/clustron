// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Models;
using Clustron.Core.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Monitoring
{
    public class ClusterMonitor : IMonitor
    {
        private readonly ClustronClientCore _core;

        public ClusterMonitor(ClustronClientCore core)
        {  
            _core = core; 
        }

        public NodeHealthStatus GetLocalHealth()
        {
            throw new NotImplementedException();
        }

        public ClusterMetrics GetMetrics()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<NodeHealthStatus> GetPeerHealth()
        {
            throw new NotImplementedException();
        }
    }
}
