// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster;
using Clustron.Core.Lifecycle;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Clustron.Core.Hosting
{
    public class ClustronNodeHost
    {
        private readonly ClusterNodeControllerBase _controller;
        private readonly ILogger<ClustronNodeHost> _logger;
        private readonly ITransport _transport;
        private readonly IMessageRouter _router;

        public ClustronNodeHost(
            ClusterNodeControllerBase controller,
            ITransport transport,
            IMessageRouter router,
            ILogger<ClustronNodeHost> logger)
        {
            _controller = controller;
            _transport = transport;
            _router = router;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _logger.LogInformation("Starting Clustron node...");

            await _transport.StartAsync(_router);
            await Task.Delay(500);
            await _controller.StartAsync();
        }

        public Task StopAsync()
        {
            _logger.LogInformation("Stopping Clustron node...");
            return Task.CompletedTask;
        }
    }
}

