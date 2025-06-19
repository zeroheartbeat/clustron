// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clustron.Core.Hosting;
public class ClusterViewLogger : IHostedService, IDisposable
{
    private readonly ILogger<ClusterViewLogger> _logger;
    private readonly ClusterPeerManager _peerManager;
    private readonly ClusterNodeControllerBase _leader;
    private readonly NodeInfo _self;
    private readonly int _intervalSeconds;
    private Timer? _timer;

    public ClusterViewLogger(
        ILogger<ClusterViewLogger> logger,
        IOptions<ClustronConfig> options, ClusterPeerManager peerManager,
        ClusterNodeControllerBase clusterLeader)
    {
        _logger = logger;
        _peerManager = peerManager;
        _leader = clusterLeader;
        _self = peerManager.Self;
        _intervalSeconds = options.Value.LogClusterViewIntervalSeconds;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_intervalSeconds > 0)
        {
            _timer = new Timer(LogClusterView, null, TimeSpan.Zero, TimeSpan.FromSeconds(_intervalSeconds));
        }
        return Task.CompletedTask;
    }

    private void LogClusterView(object? state)
    {
        var peers = _peerManager.GetActivePeers();
        var leader = _leader.CurrentLeader?.NodeId ?? "<none>";
        _logger.LogInformation("Cluster View from {NodeId}: ActivePeers=[{Peers}] Leader={Leader} Epoch={Epoch}",
            _self.NodeId,
            string.Join(", ", peers.Select(p => p.NodeId)),
            leader, _leader.CurrentEpoch);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}

