// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System.Timers;

namespace Clustron.Core.Cluster.Behaviors;

public class ClusterViewBroadcasterBehavior : IRoleAwareBehavior
{
    private readonly ILogger<ClusterViewBroadcasterBehavior> _logger;
    private readonly ClusterPeerManager _peerManager;
    private readonly IMessageSerializer _serializer;
    private readonly NodeInfo _self;
    private readonly IClusterCommunication _communication;
    private System.Timers.Timer? _timer;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(20);

    public string Name => "ClusterViewBroadcaster";

    public ClusterViewBroadcasterBehavior(
        ILogger<ClusterViewBroadcasterBehavior> logger,
        IClusterRuntime runtime,
        IMessageSerializer serializer,
        IClusterCommunication communication)
    {
        _logger = logger;
        _peerManager = runtime.PeerManager;
        _serializer = serializer;
        _self = runtime.Self;
        _communication = communication;
    }

    public Task StartAsync()
    {
        _timer = new System.Timers.Timer(BroadcastInterval.TotalMilliseconds);
        _timer.Elapsed += async (_, _) => await BroadcastClusterViewAsync();
        _timer.AutoReset = true;
        _timer.Start();

        _logger.LogInformation("ClusterViewBroadcaster started.");
        return Task.CompletedTask;
    }

    private async Task BroadcastClusterViewAsync()
    {
        var peers = _peerManager.GetActivePeers().Where(p => p.NodeId != _self.NodeId).ToList();

        if (!peers.Any())
            return;

        var view = new ClusterViewPayload
        {
            KnownPeers = peers
        };

        var message = MessageBuilder.Create(_self.NodeId, MessageTypes.ClusterView, Guid.NewGuid().ToString(), view);

        foreach (var peer in peers)
        {
            try
            {
                await _communication.Transport.SendAsync(peer, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to send cluster view to {NodeId}: {Error}", peer.NodeId, ex.Message);
            }
        }

        _logger.LogDebug("Broadcasted cluster view to {Count} peers", peers.Count);
    }

    public bool ShouldRunInRole(IList<string> roles)
            => roles.Contains(ClustronRoles.Member);
}

