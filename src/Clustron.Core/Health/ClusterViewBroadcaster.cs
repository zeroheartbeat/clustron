// Copyright (c) 2025 zeroheartbeat
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
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Health;

public class ClusterViewBroadcaster
{
    private readonly ClusterPeerManager _peerManager;
    private readonly IClusterRuntime _runtime;
    private readonly IClusterCommunication _communication;
    private readonly ILogger<ClusterViewBroadcaster> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private CancellationTokenSource? _cts;

    public ClusterViewBroadcaster(
        IClusterRuntime runtime,
        IClusterCommunication communication,
        IMessageSerializer serializer,
        ILogger<ClusterViewBroadcaster> logger)
    {
        _runtime = runtime;
        _peerManager = runtime.PeerManager;
        _communication = communication;
        _serializer = serializer;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var knownPeers = _peerManager.GetActivePeers()
                    .ToList();

                var payload = new ClusterViewPayload { KnownPeers = knownPeers };
                var message = MessageBuilder.Create<ClusterViewPayload>(_runtime.Self.NodeId, MessageTypes.ClusterView, payload);

                foreach (var peer in knownPeers)
                {
                    try
                    {
                        await _communication.Transport.SendAsync(peer, message);
                        _logger.LogDebug("Sent cluster view to {PeerId}", peer.NodeId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to send cluster view to {PeerId}: {Error}", peer.NodeId, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Cluster view broadcast failed: {Error}", ex);
            }

            await Task.Delay(_interval, token);
        }
    }
}

