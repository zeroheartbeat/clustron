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

namespace Clustron.Core.Discovery
{
    public class InMemoryDiscoveryProvider : IDiscoveryProvider
    {
        private readonly HashSet<string> _nodeIds = new();
        private readonly List<NodeInfo> _nodes = new();
        private readonly object _lock = new();

        public Task RegisterSelfAsync(NodeInfo self)
        {
            AddOrUpdate(self);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<NodeInfo>> DiscoverNodesAsync()
        {
            lock (_lock)
            {
                // Return a copy to prevent mutation
                return Task.FromResult(_nodes.ToList().AsEnumerable());
            }
        }

        public void AddOrUpdate(NodeInfo node)
        {
            lock (_lock)
            {
                if (_nodeIds.Add(node.NodeId))
                {
                    _nodes.Add(node);
                }
            }
        }
    }

}

