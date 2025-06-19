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

namespace Clustron.Core.Extensions
{
    public static class NodeInfoExtensions
    {
        public static IEnumerable<NodeInfo> ExcludeSelf(this IEnumerable<NodeInfo> nodes, string selfId)
        {
            return nodes.Where(n => n.NodeId != selfId);
        }

        public static IEnumerable<NodeInfo> ExcludeNodes(this IEnumerable<NodeInfo> nodes, params string[] excludedIds)
        {
            var excluded = excludedIds.ToHashSet();
            return nodes.Where(n => !excluded.Contains(n.NodeId));
        }

    }
}

