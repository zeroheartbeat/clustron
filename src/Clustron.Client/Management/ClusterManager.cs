// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Extensions;
using Clustron.Client.Models;
using Clustron.Core.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Management
{
    public class ClusterManager : IClusterManager
    {
        private readonly ClustronClientCore _core;

        public ClusterManager(ClustronClientCore core)  
        { 
            _core = core;

            _core.NodeJoined += node => NodeJoined?.Invoke(node.ToClusterNode());
            _core.NodeLeft += node => NodeLeft?.Invoke(node.ToClusterNode());
            _core.LeaderChanged += (leader, epoch) => LeaderChanged?.Invoke(leader.ToClusterNode(), epoch);
        }

        public ClusterNode Self => _core.Self.ToClusterNode();

        public event Action<ClusterNode> NodeJoined;
        public event Action<ClusterNode> NodeLeft;
        public event Action<ClusterNode, int> LeaderChanged;

        public int GetCurrentEpoch()
        {
            throw new NotImplementedException();
        }

        public ClusterNode? GetCurrentLeader()
            => _core.GetCurrentLeader()?.ToClusterNode();

        public IEnumerable<ClusterNode> GetPeers()
            => _core.GetMembers().Select(n => n.ToClusterNode());

        public IEnumerable<ClusterNode> GetPeersByRole(string role)
            => _core.GetPeersByRole(role).Select(n => n.ToClusterNode());

        public bool IsPeerAlive(string nodeId)
            => _core.IsPeerAlive(nodeId);

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
