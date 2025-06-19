// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Extensions;
using Clustron.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Election
{
    public class BullyElectionStrategy : IElectionStrategy
    {
        private readonly TimeSpan _electionTimeout;
        private readonly ILogger<BullyElectionStrategy> _logger;
        private readonly IClusterRuntime _clusterRuntime;

        public BullyElectionStrategy(
            IClusterRuntime clusterRuntime,
            IClusterLoggerProvider loggerProvider,
            TimeSpan? electionTimeout = null)
        {
            _clusterRuntime= clusterRuntime;
            _logger = loggerProvider.GetLogger<BullyElectionStrategy>();
            _electionTimeout = electionTimeout ?? TimeSpan.FromSeconds(3);
        }

        public async Task<NodeInfo?> ElectLeaderAsync(IEnumerable<NodeInfo> knownNodes, NodeInfo self)
        {
            var allNodes = knownNodes.Append(self).ToList();

            _logger.LogDebug($"[Election] {self.NodeId} sees total nodes: {allNodes.Count}");
            foreach (var node in allNodes)
                _logger.LogDebug($"[Election] Node in cluster: {node.NodeId}");

            var higherNodes = allNodes
                .Where(n => string.Compare(n.NodeId, self.NodeId, StringComparison.Ordinal) > 0
                        && n.HasRole(ClustronRoles.Member))
                .ToList();

            _logger.LogDebug($"[Election] {self.NodeId} sees {higherNodes.Count} higher-priority nodes.");
            foreach (var node in higherNodes)
                _logger.LogDebug($"[Election] Higher node: {node.NodeId}");

            if (!higherNodes.Any())
            {
                _logger.LogInformation($"[Election] {self.NodeId} becomes leader (no higher nodes).");
                return self;
            }

            _logger.LogDebug($"[Election] {self.NodeId} checking if higher nodes are alive...");

            var tasks = higherNodes.Select(async node =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(_electionTimeout);
                    var response = _clusterRuntime.PeerManager.IsAlive(node.NodeId);

                    _logger.LogDebug($"[Election] Response from {node.NodeId}: {(response ? "OK" : "No response")}");
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Election] Exception contacting {node.NodeId}: {ex.Message}");
                    return false;
                }
            }).ToList();

            var responses = await Task.WhenAll(tasks);
            var responsiveHigherExists = responses.Any(r => r);

            if (!responsiveHigherExists)
            {
                _logger.LogInformation($"[Election] {self.NodeId} becomes leader (no higher nodes responded).");
                return self;
            }

            _logger.LogInformation($"[Election] {self.NodeId} will not become leader; higher nodes responded.");
            return null;
        }
    }
}
