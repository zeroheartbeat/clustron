// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Configuration;
using Clustron.Core.Discovery;
using Clustron.Core.Election;
using Clustron.Core.Events;
using Clustron.Core.Models;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Cluster
{
    public class ClusterContext : IClusterRuntime, IClusterCommunication, IClusterLoggerProvider,
                                    IElectionCoordinatorProvider, IClusterDiscovery
    {
        private ITransport _transport;
        private ElectionCoordinator _coordinator;
        private readonly ClustronConfig _clustronConfig;

        public NodeInfo Self { get; }
        public ITransport Transport => _transport;
        public IDiscoveryProvider DiscoveryProvider { get; }
        public ILoggerFactory LoggerFactory { get; }

        public ClusterPeerManager PeerManager { get; }
        public IClusterEventBus EventBus { get; }
        public ElectionCoordinator Coordinator => _coordinator;

        public ClustronConfig Configuration => _clustronConfig;

        public ClusterContext(
            NodeInfo self,
            ITransport transport,
            IDiscoveryProvider discoveryProvider,
            ClusterPeerManager peerManager,
            IClusterEventBus eventBus,
            ClustronConfig clustronConfig,
            ILoggerFactory loggerFactory)
        {
            Self = self;
            _transport = transport;
            DiscoveryProvider = discoveryProvider;
            PeerManager = peerManager;
            _clustronConfig = clustronConfig;
            EventBus = eventBus;
            LoggerFactory = loggerFactory;
        }

        public void OverrideTransport(ITransport realTransport)
        {
            _transport = realTransport;
        }

        public void SetElectionCoordinatory(ElectionCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public ILogger<T> GetLogger<T>() => LoggerFactory.CreateLogger<T>();
    }
}

