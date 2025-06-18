// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster;
using Clustron.Core.Events;
using Clustron.Core.Health;
using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Clustron.Core.Health
{
    public class HeartbeatNodeTracker
    {
        private readonly IHeartbeatMonitor _heartbeatMonitor;
        private readonly NodeInfo _self;
        private readonly ILogger<HeartbeatNodeTracker> _logger;
        private readonly ConcurrentDictionary<string, bool> _trackedNodes = new();

        public HeartbeatNodeTracker(IClusterEventBus bus, IHeartbeatMonitor monitor, NodeInfo self, IClusterLoggerProvider loggerProvider)
        {
            _heartbeatMonitor = monitor;
            _self = self;
            _logger = loggerProvider.GetLogger<HeartbeatNodeTracker>();

            bus.Subscribe<NodeJoinedEvent>(e =>
            {
                if (e.Node.NodeId != self.NodeId)
                    monitor.AddPeer(e.Node);
            });

            bus.Subscribe<NodeLeftEvent>(e =>
            {
                monitor.RemovePeer(e.Node);
            });
        }

        public async Task OnNodeJoinedAsync(NodeInfo node)
        {
            _logger.LogDebug($"OnNodeJoinedAsync called for Node {node.NodeId}");
            if (node.NodeId == _self.NodeId || _trackedNodes.ContainsKey(node.NodeId))
                return;

            _trackedNodes[node.NodeId] = true;

            _heartbeatMonitor.AddPeer(node);
        }

        public Task OnNodeLeftAsync(NodeInfo node)
        {
            _heartbeatMonitor.RemovePeer(node); 
            _trackedNodes.TryRemove(node.NodeId, out _);
            return Task.CompletedTask;
        }
    }
}

