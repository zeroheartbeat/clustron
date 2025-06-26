// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Configuration;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Cluster.Behaviors
{
    public class MetricsCollectorBehavior : IRoleAwareBehavior, IMetricsListener
    {
        private readonly ILogger<MetricsCollectorBehavior> _logger;
        private readonly ClusterPeerManager _peerManager;
        private readonly IMessageSerializer _serializer;
        private readonly NodeInfo _self;
        private readonly IClusterCommunication _communication;
        private readonly ClustronConfig _configuration;
        private readonly ConcurrentDictionary<string, List<ClusterMetricsSnapshot>> _metricsHistory = new();
        private Timer? _timer;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RetentionDuration = TimeSpan.FromHours(1);

        public string Name => "MetricsCollector";

        public MetricsCollectorBehavior(
            ILogger<MetricsCollectorBehavior> logger,
            IClusterRuntime runtime,
            IMessageSerializer serializer,
            IClusterCommunication communication)
        {
            _logger = logger;
            _peerManager = runtime.PeerManager;
            _serializer = serializer;
            _self = runtime.Self;
            _communication = communication;
            _configuration = runtime.Configuration;
        }

        public bool ShouldRunInRole(IList<string> roles)
            => roles.Contains(ClustronRoles.MetricsCollector);

        public Task StartAsync()
        {
            _timer = new Timer(_ => PollMembersAsync().Wait(), null, TimeSpan.Zero, PollInterval);
            _logger.LogInformation("MetricsCollectorBehavior started.");
            return Task.CompletedTask;
        }

        private async Task PollMembersAsync()
        {
            var peers = _peerManager.GetPeersWithRole(ClustronRoles.Member)
                .Where(p => p.NodeId != _self.NodeId)
                .ToList();

            _logger.LogCritical("Polling {Count} members for metrics...", peers.Count);

            foreach (var peer in peers)
            {
                _logger.LogInformation("Polling metrics from peer: {NodeId} Roles={Roles}", peer.NodeId, string.Join(",", peer.Roles));
                var correlationId = Guid.NewGuid().ToString();
                var requestPayload = new MetricsRequest
                {
                    DurationSeconds = Convert.ToInt32(PollInterval.TotalSeconds) // or any other duration you'd like
                };

                var request = MessageBuilder.Create<MetricsRequest>(_self.NodeId, MessageTypes.RequestMetrics, correlationId, requestPayload);

                try
                {
                    await _communication.Transport.SendAsync(peer, request);

                    var response = await _communication.Transport.WaitForResponseAsync(peer.NodeId, request.CorrelationId, TimeSpan.FromSeconds(3));

                    var snapshot = _serializer.Deserialize<ClusterMetricsSnapshot>(response.Payload);
                    if (snapshot != null)
                        OnMetricsReceived(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to fetch metrics from {NodeId}: {Error}", peer.NodeId, ex.Message);
                }
            }
        }

        public void OnMetricsReceived(ClusterMetricsSnapshot snapshot)
        {
            var list = _metricsHistory.GetOrAdd(snapshot.NodeId, _ => new List<ClusterMetricsSnapshot>());
            var now = DateTime.UtcNow;

            lock (list)
            {
                list.Add(snapshot);
                list.RemoveAll(s => s.TimestampUtc < now - RetentionDuration);
            }

            // Extract totals
            snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Sent, out var totalDirectSent);
            snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Broadcasted, out var totalDirectBroadcasted);
            snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Received, out var totalDirectReceived);

            snapshot.Totals.TryGetValue(MetricKeys.Msg.Events.Published, out var totalEventsPublished);
            snapshot.Totals.TryGetValue(MetricKeys.Msg.Events.Delivered, out var totalEventsReceived);

            snapshot.Totals.TryGetValue(MetricKeys.Msg.Wire.Sent, out var totalSent);
            snapshot.Totals.TryGetValue(MetricKeys.Msg.Wire.Received, out var totalRecv);

            snapshot.Totals.TryGetValue(MetricKeys.Heartbeat.Sent, out var totalHbSent);
            snapshot.Totals.TryGetValue(MetricKeys.Heartbeat.Received, out var totalHbRecv);

            // Extract per-second rates
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Direct.Sent, out var directSentRates);
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Direct.Broadcasted, out var directBroadcastedRates);
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Direct.Received, out var directReceivedRates);

            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Events.Published, out var eventsPublishedRates);
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Events.Delivered, out var eventsReceivedRates);

            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Wire.Sent, out var sentRates);
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Msg.Wire.Received, out var recvRates);

            snapshot.PerSecondRates.TryGetValue(MetricKeys.Heartbeat.Sent, out var hbSentRates);
            snapshot.PerSecondRates.TryGetValue(MetricKeys.Heartbeat.Received, out var hbRecvRates);

            _logger.LogCritical(
                @"Received metrics from {NodeId}:

--- Direct Messages ---
  Sent:       {DirectSent}     Rate: {DirectSentRate}
  Broadcasted:{DirectBroadcasted} Rate: {DirectBroadcastedRate}
  Received:   {DirectReceived} Rate: {DirectReceivedRate}

--- Events ---
  Published:  {EventsPublished} Rate: {EventsPublishedRate}
  Delivered:  {EventsReceived} Rate: {EventsReceivedRate}

--- Wire Messages ---
  Sent:       {TotalSent}      Rate: {SentRate}
  Received:   {TotalRecv}      Rate: {RecvRate}

--- Heartbeats ---
  Sent:       {HbSent}         Rate: {HbSentRate}
  Received:   {HbRecv}         Rate: {HbRecvRate}
",
                snapshot.NodeId,

                totalDirectSent, FormatRates(directSentRates ?? Array.Empty<int>()),
                totalDirectBroadcasted, FormatRates(directBroadcastedRates ?? Array.Empty<int>()),
                totalDirectReceived, FormatRates(directReceivedRates ?? Array.Empty<int>()),

                totalEventsPublished, FormatRates(eventsPublishedRates ?? Array.Empty<int>()),
                totalEventsReceived, FormatRates(eventsReceivedRates ?? Array.Empty<int>()),

                totalSent, FormatRates(sentRates ?? Array.Empty<int>()),
                totalRecv, FormatRates(recvRates ?? Array.Empty<int>()),

                totalHbSent, FormatRates(hbSentRates ?? Array.Empty<int>()),
                totalHbRecv, FormatRates(hbRecvRates ?? Array.Empty<int>())
            );
        }



        private static string FormatRates(int[] rates)
        {
            if (rates.Length == 0)
                return "(no data)";
            return $"[{string.Join(", ", rates)}]/s";
        }

    }

}

