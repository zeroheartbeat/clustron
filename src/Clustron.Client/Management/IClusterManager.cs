// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Management
{
    public interface IClusterManager
    {
        Task StartAsync();
        Task StopAsync();
        ClusterNode Self { get; }
        ClusterNode? GetCurrentLeader();
        int GetCurrentEpoch();
        IEnumerable<ClusterNode> GetPeers();

        IEnumerable<ClusterNode> GetPeersByRole(string role);
        bool IsPeerAlive(string nodeId);

        // Cluster events
        event Action<ClusterNode> NodeJoined;
        event Action<ClusterNode> NodeLeft;
        event Action<ClusterNode, int> LeaderChanged;
    }
}
