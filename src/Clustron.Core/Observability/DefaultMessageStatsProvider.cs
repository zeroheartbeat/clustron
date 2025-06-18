// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Models;
using System.Linq;
using System.Threading;

namespace Clustron.Core.Observability;

public class DefaultMessageStatsProvider : IMetricContributor, IMetricsSnapshotProvider
{
    private readonly RollingMetricsRegistry _metrics;
    private readonly ClusterPeerManager _peerManager;
    private readonly IClusterRuntime _clusterRuntime;

    public DefaultMessageStatsProvider(IClusterRuntime clusterRuntime)
    {
        _clusterRuntime = clusterRuntime;
        _peerManager = clusterRuntime.PeerManager;
        _metrics = new RollingMetricsRegistry(clusterRuntime.Configuration.Metrics.RollingWindowSeconds);
    }

    public void Increment(string key) => _metrics.Increment(key);

    public int GetTotal(string key) => _metrics.GetTotal(key);

    public int[] GetPerSecondRates(string key, int seconds) => _metrics.GetPerSecondRates(key, seconds);

    public ClusterMetricsSnapshot CaptureSnapshot(int durationSeconds)
    {
        var snapshot = _metrics.CaptureSnapshot(durationSeconds);
        snapshot.NodeId = _clusterRuntime.Self.NodeId;
        snapshot.TimestampUtc = DateTime.UtcNow;
        snapshot.ActiveConnections = _peerManager.GetActivePeers().Count();
        return snapshot;
    }
}

