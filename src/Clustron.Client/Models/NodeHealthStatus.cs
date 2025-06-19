// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Models
{
    public class NodeHealthStatus
    {
        public string NodeId { get; set; } = string.Empty;
        public bool IsAlive { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    }
}
