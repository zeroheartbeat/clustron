// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Observability;

public class ClusterMetricsReceiver : IMetricsListener
{
    private readonly ILogger<ClusterMetricsReceiver> _logger;
    private readonly ConcurrentBag<IMetricsListener> _listeners = new();

    public ClusterMetricsReceiver(ILogger<ClusterMetricsReceiver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// This method is invoked by MetricsMessageHandler when a new metrics message arrives.
    /// </summary>
    public void OnMetricsReceived(ClusterMetricsSnapshot metrics)
    {
        _logger.LogDebug("Received metrics from node {NodeId} at {Timestamp}", metrics.NodeId, metrics.TimestampUtc);

        foreach (var listener in _listeners)
        {
            try
            {
                listener.OnMetricsReceived(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching metrics to listener.");
            }
        }
    }

    public void RegisterListener(IMetricsListener listener)
    {
        _listeners.Add(listener);
        _logger.LogDebug("Registered IMetricsListener: {Type}", listener.GetType().FullName);
    }
}

