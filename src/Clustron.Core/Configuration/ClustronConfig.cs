// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Models;

namespace Clustron.Core.Configuration;
public class ClustronConfig
{
    public string ClusterId { get; set; } = "default-cluster";
    public string Version { get; set; } = "1.0.0";
    public int Port { get; set; }
    public List<NodeInfo>? StaticNodes { get; set; }
    public int LogClusterViewIntervalSeconds { get; set; } = 0;
    public bool UseDuplexConnections { get; set; } = true;

    /// <summary>
    /// Optional list of roles this node is allowed to play (e.g., "leader", "metrics-only", etc.)
    /// </summary>
    public List<string> Roles { get; set; } = new();

    public MetricsOptions Metrics { get; set; } = new();
    public RetryOptions RetryOptions { get; set; } = new();
}

