// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

// File: Internal/NodeMapper.cs
using Clustron.Client.Models;
using Clustron.Core.Models;

namespace Clustron.Client.Internal;

internal static class NodeMapper
{
    public static ClusterNode ToClient(NodeInfo core) => new()
    {
        NodeId = core.NodeId,
        Host = core.Host,
        Port = core.Port,
        Roles = core.Roles ?? new List<string>()
    };

    public static NodeInfo ToCore(ClusterNode client) => new()
    {
        NodeId = client.NodeId,
        Host = client.Host,
        Port = client.Port,
        Roles = client.Roles.ToList()
    };
}

