// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Transport;

namespace Clustron.Core.Health
{
    public interface IHeartbeatMonitor
    {
        Task StartAsync(NodeInfo self, IEnumerable<NodeInfo> peers);
        event Func<NodeInfo, Task> OnNodeFailed;
        //bool IsAlive(string nodeId);
        void AddPeer(NodeInfo peer);
        Task RemovePeer(NodeInfo peer);
        void SetClusterContext(Lazy<ClusterNodeControllerBase> controller, Lazy<ITransport> transport);
        Task MarkNodeLeft(NodeInfo node);
        void MarkHeartbeatReceived(string nodeId);

    }
}
