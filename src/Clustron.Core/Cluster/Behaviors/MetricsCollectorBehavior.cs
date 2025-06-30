using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Cluster.Behaviors;
using Clustron.Core.Configuration;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Clustron.Core.Cluster.Behaviors;

public class MetricsCollectorBehavior : IRoleAwareBehavior, IMetricsListener
{
    private readonly ILogger<MetricsCollectorBehavior> _logger;
    private readonly ClusterPeerManager _peerManager;
    private readonly IMessageSerializer _serializer;
    private readonly NodeInfo _self;
    private readonly IClusterCommunication _communication;
    private readonly ClustronConfig _configuration;
    private readonly ConcurrentDictionary<string, List<ClusterMetricsSnapshot>> _metricsHistory = new();
    private readonly CancellationTokenSource _cts = new();

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetentionDuration = TimeSpan.FromHours(1);

    private int _isPolling = 0;

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
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                await PollMembersAsync();
            }
        }, _cts.Token);

        _logger.LogInformation("MetricsCollectorBehavior started.");
        return Task.CompletedTask;
    }

    private async Task PollMembersAsync()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1)
            return;

        try
        {
            var peers = _peerManager.GetPeersWithRole(ClustronRoles.Member)
                .Where(p => p.NodeId != _self.NodeId)
                .ToList();

            _logger.LogInformation("Polling {Count} members for metrics...", peers.Count);

            var tasks = peers.Select(PollPeerAsync).ToList();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during polling.");
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private async Task PollPeerAsync(NodeInfo peer)
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = MessageBuilder.Create<MetricsRequest>(
            _self.NodeId,
            MessageTypes.MetricsRequest,
            correlationId,
            new MetricsRequest { DurationSeconds = (int)PollInterval.TotalSeconds });

        var timeout = TimeSpan.FromMilliseconds(_configuration.Metrics.MetricsTimeoutMilliseconds);

        try
        {
            await _communication.Transport.SendImmediateAsync(peer, request);

            var responseTask = _communication.Transport.WaitForResponseAsync(peer.NodeId, correlationId, timeout);
            var delayTask = Task.Delay(timeout);

            var completedTask = await Task.WhenAny(responseTask, delayTask);

            if (completedTask != responseTask)
            {
                throw new TimeoutException("Timed out waiting for metrics response.");
            }

            if (responseTask.IsFaulted)
            {
                throw new Exception("Request failed.", responseTask.Exception);
            }

            if (responseTask.IsCanceled)
            {
                throw new OperationCanceledException("Request was canceled.");
            }

            var response = await responseTask;
            var snapshot = _serializer.Deserialize<ClusterMetricsSnapshot>(response.Payload);

            if (snapshot != null)
                OnMetricsReceived(snapshot);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for metrics from {NodeId}", peer.NodeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to fetch metrics from {NodeId}: {Error}", peer.NodeId, ex.Message);
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

        LogMetrics(snapshot);
    }

    private void LogMetrics(ClusterMetricsSnapshot snapshot)
    {
        snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Sent, out var totalDirectSent);
        snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Broadcasted, out var totalDirectBroadcasted);
        snapshot.Totals.TryGetValue(MetricKeys.Msg.Direct.Received, out var totalDirectReceived);

        snapshot.Totals.TryGetValue(MetricKeys.Msg.Events.Published, out var totalEventsPublished);
        snapshot.Totals.TryGetValue(MetricKeys.Msg.Events.Delivered, out var totalEventsReceived);

        snapshot.Totals.TryGetValue(MetricKeys.Msg.Wire.Sent, out var totalSent);
        snapshot.Totals.TryGetValue(MetricKeys.Msg.Wire.Received, out var totalRecv);

        snapshot.Totals.TryGetValue(MetricKeys.Heartbeat.Sent, out var totalHbSent);
        snapshot.Totals.TryGetValue(MetricKeys.Heartbeat.Received, out var totalHbRecv);

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
            totalDirectSent, FormatRates(directSentRates),
            totalDirectBroadcasted, FormatRates(directBroadcastedRates),
            totalDirectReceived, FormatRates(directReceivedRates),
            totalEventsPublished, FormatRates(eventsPublishedRates),
            totalEventsReceived, FormatRates(eventsReceivedRates),
            totalSent, FormatRates(sentRates),
            totalRecv, FormatRates(recvRates),
            totalHbSent, FormatRates(hbSentRates),
            totalHbRecv, FormatRates(hbRecvRates)
        );
    }

    private static string FormatRates(int[]? rates)
    {
        return (rates == null || rates.Length == 0) ? "(no data)" : $"[{string.Join(", ", rates)}]/s";
    }
}