﻿// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Handlers
{
    public class MetricsRequestHandler : IMessageHandler
    {
        private readonly ILogger<MetricsRequestHandler> _logger;
        private readonly IMetricsSnapshotProvider _snapshotProvider;
        private readonly IMessageSerializer _serializer;
        private readonly IClusterRuntime _runtime;
        private readonly IClusterCommunication _communication;

        public string Type => MessageTypes.MetricsRequest;

        public MetricsRequestHandler(
            ILogger<MetricsRequestHandler> logger,
            IMetricsSnapshotProvider snapshotProvider,
            IMessageSerializer serializer,
            IClusterRuntime clusterRuntime,
            IClusterCommunication communication)
        {
            _logger = logger;
            _snapshotProvider = snapshotProvider;
            _serializer = serializer;
            _runtime = clusterRuntime;
            _communication = communication;
        }

        public async Task HandleAsync(Message message)
        {
            MetricsRequest request;

            try
            {
                request = _serializer.Deserialize<MetricsRequest>(message.Payload);
            }
            catch
            {
                request = new MetricsRequest(); // fallback to default
            }

            var sw = Stopwatch.StartNew();
            var snapshot = _snapshotProvider.CaptureSnapshot(request.DurationSeconds);
            sw.Stop();
            _logger.LogCritical("Captured snapshot in {Elapsed}ms", sw.ElapsedMilliseconds);
            snapshot.TimestampUtc = DateTime.UtcNow;

            var reply = MessageBuilder.Create<ClusterMetricsSnapshot>(_runtime.Self.NodeId, MessageTypes.MetricsResponse, message.CorrelationId, snapshot);

            var requester = _runtime.PeerManager.GetPeerById(message.SenderId);
            if (requester != null)
            {
                await _communication.Transport.SendImmediateAsync(requester, reply);
                _logger.LogCritical("Sent metrics to {NodeId} for last {Duration}s", requester.NodeId, request.DurationSeconds);
            }
        }

    }

}

