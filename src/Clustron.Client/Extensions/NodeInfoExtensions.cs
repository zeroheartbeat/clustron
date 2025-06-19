// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Models;
using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Clustron.Client.Extensions
{
    public static class NodeInfoExtensions
    {
        public static ClusterNode ToClusterNode(this NodeInfo node)
        {
            return new ClusterNode
            {
                NodeId = node.NodeId,
                Host = node.Host,
                Port = node.Port,
                Roles = node.Roles,
            };
        }
    }
}
