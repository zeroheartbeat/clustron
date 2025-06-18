// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;

namespace Clustron.Core.Health;

public class TcpHeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ILogger<TcpHeartbeatMonitor> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(12);
    private readonly IMessageSerializer _serializer;
    private readonly ClusterPeerManager _peerManager;
    private readonly IMetricContributor _metrics;

    private NodeInfo _self = default!;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private Lazy<ClusterNodeControllerBase> _clusterLeader;
    private ITransport _transport;

    public event Func<NodeInfo, Task>? OnNodeFailed;

    public TcpHeartbeatMonitor(IClusterRuntime clusterRuntime, IMessageSerializer serializer, IMetricContributor metrics, 
                            IClusterLoggerProvider loggerProvider)
    {
        _logger = loggerProvider.GetLogger<TcpHeartbeatMonitor>();
        _serializer = serializer;
        _peerManager = clusterRuntime.PeerManager;
        _metrics = metrics;
    }

    public void SetClusterContext(Lazy<ClusterNodeControllerBase> controller, Lazy<ITransport> transport)
    {
        _clusterLeader = controller;
        _transport = transport.Value;
    }


    public Task StartAsync(NodeInfo self, IEnumerable<NodeInfo> peers)
    {
        _self = self;

        foreach (var peer in peers.Where(p => p.NodeId != self.NodeId))
        {
            _peerManager.RegisterPeer(peer);
        }

        RestartMonitorLoop();
        _logger.LogInformation("Heartbeat monitor started.");
        return Task.CompletedTask;
    }

    public void AddPeer(NodeInfo peer)
    {
        _peerManager.RegisterPeer(peer);
        RestartMonitorLoop();
    }

    public async Task RemovePeer(NodeInfo peer)
    {
        _peerManager.MarkPeerDown(peer);

        _transport.RemoveConnection(peer.NodeId);

        if (OnNodeFailed != null)
            await OnNodeFailed.Invoke(peer);
    }


    public async Task MarkNodeLeft(NodeInfo node)
    {
        _logger.LogWarning("Removing node from heartbeat tracking: {NodeId}", node.NodeId);
        await RemovePeer(node);
        _peerManager.MarkPeerDown(node);
    }

    //public bool IsAlive(string nodeId) => _peerRegistry.IsAlive(nodeId);

    public void MarkHeartbeatReceived(string nodeId)
    {
        _metrics.Increment(MetricKeys.Heartbeat.Received);
        _peerManager.MarkHeartbeatReceived(nodeId);
    }

    private void RestartMonitorLoop()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    private async Task MonitorLoop(CancellationToken token)
    {
        int printInterval = 0;

        while (!token.IsCancellationRequested)
        {
            printInterval++;

            var peersSnapshot = _peerManager
                .GetActivePeers()
                .Where(p => p.NodeId != _self.NodeId)
                .ToList();

            if (printInterval % 50 == 0)
            {
                foreach (var peer in peersSnapshot)
                {
                    var memberInfo = _clusterLeader.Value.CurrentLeader?.NodeId == peer.NodeId ? "Leader" : "Member";
                    _logger.LogDebug("Peer {NodeId} : {Role}", peer.NodeId, memberInfo);
                }
            }

            foreach (var peer in peersSnapshot)
            {
                var missCount = _peerManager.GetMissCount(peer.NodeId);
                _logger.LogDebug("Missed heartbeat count for {NodeId}: {Count}", peer.NodeId, missCount);

                var isAlive = await SendHeartbeatAsync(peer);

                if (isAlive)
                {
                    _logger.LogDebug("Received heartbeat from {NodeId} (ping successful)", peer.NodeId);
                    _peerManager.MarkHeartbeatReceived(peer.NodeId);
                }
                else
                {
                    _peerManager.RecordMissedHeartbeat(peer.NodeId);

                    if (_peerManager.GetMissCount(peer.NodeId) == 1)
                    {
                        _logger.LogWarning("Suspecting node {NodeId} due to missed heartbeat.", peer.NodeId);

                        if (_clusterLeader.Value.CurrentLeader != null)
                        {
                            var msg = new Message
                            {
                                MessageType = MessageTypes.HeartbeatSuspect,
                                SenderId = _self.NodeId,
                                Payload = _serializer.Serialize(peer.NodeId)
                            };
                            _ = _transport.SendAsync(_clusterLeader.Value.CurrentLeader, msg);
                        }
                    }

                    if (_peerManager.HasTimedOut(peer.NodeId, _timeout) && _peerManager.IsAlive(peer.NodeId))
                    {
                        _logger.LogWarning("Node failed: {NodeId}", peer.NodeId);
                        await RemovePeer(peer);
                        await _transport.HandlePeerDownAsync(peer.NodeId);
                    }
                }
            }

            try
            {
                await Task.Delay(_interval, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }


    private async Task<bool> SendHeartbeatAsync(NodeInfo node)
    {
        try
        {
            var heartbeatPayload = new HeartbeatPayload 
            { 
                LeaderId = _clusterLeader.Value.CurrentLeader.NodeId, 
                LeaderEpoch = _clusterLeader.Value.CurrentEpoch 
            };

            var correlationId = Guid.NewGuid().ToString();
            var message = MessageBuilder.Create(_self.NodeId, MessageTypes.Heartbeat, correlationId, heartbeatPayload);

            await _transport!.SendAsync(node, message);
            _metrics.Increment(MetricKeys.Heartbeat.Sent);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to send heartbeat to {NodeId}: {Error}", node.NodeId, ex.Message);
            return false;
        }
    }

}

