// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Extensions;
using Clustron.Core.Messaging;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Handlers;

public class MetricsSnapshotReceiverHandler : IMessageHandler
{
    private readonly ILogger<MetricsSnapshotReceiverHandler> _logger;
    private readonly IEnumerable<IMetricsListener> _listeners;
    private readonly IMessageSerializer _serializer;
    private readonly IClusterRuntime _runtime;

    public string Type => MessageTypes.ClustronMetrics;

    public MetricsSnapshotReceiverHandler(
        IClusterRuntime runtime,
        IEnumerable<IMetricsListener> listeners,
        IMessageSerializer serializer,
        ILogger<MetricsSnapshotReceiverHandler> logger)
    {
        _listeners = listeners;
        _serializer = serializer;
        _runtime = runtime;
        _logger = logger;
    }

    public Task HandleAsync(Message message)
    {
        if(!_runtime.Self.IsMetricsCollector())
            return Task.CompletedTask;

        var snapshot = _serializer.Deserialize<ClusterMetricsSnapshot>(message.Payload);
        if (snapshot == null)
        {
            _logger.LogWarning("Received null metrics snapshot.");
            return Task.CompletedTask;
        }

        foreach (var listener in _listeners)
            listener.OnMetricsReceived(snapshot);

        return Task.CompletedTask;
    }
}

