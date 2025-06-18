// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Abstractions;

public class ClusterMetricsSnapshot
{
    public string NodeId { get; set; }
    public DateTime TimestampUtc { get; set; }

    public int ActiveConnections { get; set; }

    public Dictionary<string, int> Totals { get; set; } = new();
    public Dictionary<string, int[]> PerSecondRates { get; set; } = new();

    // Optional single-value metrics (e.g., for gauges)
    public Dictionary<string, double> CustomMetrics { get; set; } = new();

}


