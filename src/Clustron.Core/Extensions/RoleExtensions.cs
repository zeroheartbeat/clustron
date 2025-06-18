// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Configuration;
using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Extensions
{
    public static class RoleExtensions
    {
        public static bool HasRole(this NodeInfo node, string role) =>
            node.Roles?.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)) == true;
        public static bool IsMember(this NodeInfo node) => node.HasRole(ClustronRoles.Member);
        public static bool IsObserver(this NodeInfo node) => node.HasRole(ClustronRoles.Observer);
        public static bool IsMetricsCollector(this NodeInfo node) => node.HasRole(ClustronRoles.MetricsCollector);
        public static bool IsClient(this NodeInfo node) => node.HasRole(ClustronRoles.Client);
    }
}

