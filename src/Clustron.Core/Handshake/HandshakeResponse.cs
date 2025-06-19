// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Handshake
{
    public class HandshakeResponse
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; } = "";
        public NodeInfo? Leader { get; set; }

        public NodeInfo ResponderNode { get; set; }

        public int LeaderEpoch { get; set; } = 0;
    }

}

