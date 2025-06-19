// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

    using Clustron.Core.Configuration;
    using Clustron.Core.Discovery;
    using Clustron.Core.Extensions;
    using Clustron.Core.Handshake;
    using Clustron.Core.Helpers;
    using Clustron.Core.Models;
    using Clustron.Core.Transport;
    using Microsoft.Extensions.Logging;

    namespace Clustron.Core.Cluster;

    public class JoinManager
    {
        private readonly ILogger<JoinManager> _logger;
        private readonly ClustronConfig _config;
        private readonly IHandshakeProtocol _handshakeProtocol;
        private readonly ClusterPeerManager _peerManager;
        private readonly IClusterDiscovery _clusterDiscovery;

        public JoinManager(IClusterRuntime clusterRuntime, IClusterDiscovery clusterDiscovery,
            IHandshakeProtocol handshakeProtocol, ClusterPeerManager peerManager,
            ClustronConfig config, IClusterLoggerProvider loggerProvider)
        { 
            _handshakeProtocol = handshakeProtocol;
            _peerManager = clusterRuntime.PeerManager;
            _config = config;
            _clusterDiscovery = clusterDiscovery;

            _logger = loggerProvider.GetLogger<JoinManager>();
        }

        public async Task<JoinResult> JoinClusterAsync(NodeInfo self)
        {
            _logger.LogInformation("Registering self to discovery...");
            await _clusterDiscovery.DiscoveryProvider.RegisterSelfAsync(self);

            _logger.LogInformation("Discovering peer nodes...");
            var discoveredPeers = (await _clusterDiscovery.DiscoveryProvider.DiscoverNodesAsync()).ExcludeSelf(self.NodeId).ToList();

            _logger.LogInformation("Discovered peers: {Peers}", string.Join(", ", discoveredPeers.Select(p => $"{p.NodeId} ({p.Host}:{p.Port})")));

            _logger.LogInformation("Discovered {Count} peers: {Peers}",
                discoveredPeers.Count,
                string.Join(", ", discoveredPeers.Select(p => p.NodeId)));

            var acceptedPeers = new List<NodeInfo>();
            NodeInfo? knownLeader = null;
            int highestEpoch = 0;

            foreach (var peer in discoveredPeers)
            {
                try
                {
                    _logger.LogDebug("Attempting handshake with {Peer}", peer.NodeId);
                    var result = await RetryHelper.RetryAsync(
                            () => _handshakeProtocol.InitiateHandshakeAsync(peer),
                            _config.RetryOptions.MaxAttempts,
                            _config.RetryOptions.DelayMilliseconds,
                            _logger);

                    _logger.LogDebug("Handshake result from {Peer}: Accepted={Accepted}, Leader={Leader}",
                        peer.NodeId,
                        result.Accepted,
                        result.Leader?.NodeId ?? "none");


                    if (result.Accepted)
                    {
                        _logger.LogInformation("Handshake successful with {Peer}", peer.NodeId);

                        _peerManager.RegisterPeer(result.ResponderNode); 

                        acceptedPeers.Add(result.ResponderNode);

                        if (result.Leader != null && result.LeaderEpoch > highestEpoch)
                        {
                            knownLeader = result.Leader;
                            highestEpoch = result.LeaderEpoch;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Handshake rejected by {Peer}: {Reason}", peer.NodeId, result.Reason);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to handshake with {Peer}: {Error}", peer.NodeId, ex.Message);
                }
            }

            _logger.LogInformation("Total accepted peers after join: {Count}", acceptedPeers.Count);
            return new JoinResult(acceptedPeers, knownLeader, highestEpoch);
        }
    }

