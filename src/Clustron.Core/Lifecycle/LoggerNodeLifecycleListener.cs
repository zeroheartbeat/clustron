// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Events;
using Clustron.Core.Lifecycle;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Clustron.Core.Lifecycle
{
    public class LoggingNodeLifecycleListener
    {
        private readonly ILogger<LoggingNodeLifecycleListener> _logger;

        public LoggingNodeLifecycleListener(IClusterEventBus bus, ILogger<LoggingNodeLifecycleListener> logger)
        {
            _logger = logger;

            bus.Subscribe<NodeJoinedEvent>(e => _logger.LogInformation("Node joined: {NodeId}", e.Node.NodeId));
            bus.Subscribe<NodeLeftEvent>(e => _logger.LogWarning("Node left: {NodeId}", e.Node.NodeId));
        }
    }
}

