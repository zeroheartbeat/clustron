// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Configuration;
using Clustron.Core.Health;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster.Behaviors
{
    public class HeartbeatMonitorBehavior : IRoleAwareBehavior
    {
        private readonly IHeartbeatMonitor _heartbeat;
        private readonly NodeInfo _self;
        private readonly ClusterPeerManager _peerManager;
        private readonly ILogger<HeartbeatMonitorBehavior> _logger;

        public string Name => "HeartbeatMonitor";

        public HeartbeatMonitorBehavior(
            IHeartbeatMonitor heartbeat,
            NodeInfo self,
            ClusterPeerManager peerManager,
            ILogger<HeartbeatMonitorBehavior> logger)
        {
            _heartbeat = heartbeat;
            _self = self;
            _peerManager = peerManager;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _logger.LogInformation("Starting heartbeat monitor...");
            await _heartbeat.StartAsync(_self, _peerManager.GetActivePeers());
        }

        public bool ShouldRunInRole(IList<string> roles)
            => roles.Contains(ClustronRoles.Member);

    }

}

