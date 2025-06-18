// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Discovery;
using Clustron.Core.Models;

namespace Clustron.Core.Discovery;

public class StaticDiscoveryProvider : IDiscoveryProvider
{
    private readonly List<NodeInfo> _nodes;

    public StaticDiscoveryProvider(List<NodeInfo> nodes)
    {
        _nodes = nodes;
    }

    public Task<IEnumerable<NodeInfo>> DiscoverNodesAsync()
    {
        return Task.FromResult(_nodes.AsEnumerable());
    }

    public Task RegisterSelfAsync(NodeInfo self)
    {
        return Task.CompletedTask;
    }
}

