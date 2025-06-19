// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using System.Collections.Concurrent;

namespace Clustron.Core.Observability;

public class RollingMetricsRegistry : IMetricsSnapshotProvider
{
    private readonly ConcurrentDictionary<string, RollingCounter> _counters = new();
    private readonly int _windowSize;

    public RollingMetricsRegistry(int windowSize)
    {
        _windowSize = windowSize;
    }

    public void Increment(string key)
    {
        var counter = _counters.GetOrAdd(key, _ => new RollingCounter(_windowSize));
        counter.Increment();
    }

    public long GetTotal(string key)
    {
        if (_counters.TryGetValue(key, out var counter))
        {
            return counter.Total();
        }
        return 0;
    }

    public int[] GetPerSecondRates(string key, int seconds)
    {
        if (_counters.TryGetValue(key, out var counter))
        {
            return counter.GetPerSecondRates(seconds);
        }
        return Enumerable.Repeat(0, seconds).ToArray();
    }

    public ClusterMetricsSnapshot CaptureSnapshot(int seconds)
    {
        var snapshot = new ClusterMetricsSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            Totals = new Dictionary<string, long>(),
            PerSecondRates = new Dictionary<string, int[]>()
        };

        foreach (var kvp in _counters)
        {
            snapshot.Totals[kvp.Key] = kvp.Value.Total();
            snapshot.PerSecondRates[kvp.Key] = kvp.Value.GetPerSecondRates(seconds);
        }

        return snapshot;
    }
}

