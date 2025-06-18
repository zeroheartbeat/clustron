// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Cluster.Behaviors;
using Clustron.Core.Cluster.State;
using Clustron.Core.Configuration;
using Clustron.Core.Discovery;
using Clustron.Core.Election;
using Clustron.Core.Events;
using Clustron.Core.Handlers;
using Clustron.Core.Handshake;
using Clustron.Core.Health;
using Clustron.Core.Hosting;
using Clustron.Core.Lifecycle;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Clustron.Core.Hosting;
public static class InternalServiceRegistration
{
    public static IServiceCollection AddClustronCoreServices(
    this IServiceCollection services,
    ClustronConfig config,
    NodeInfo self,
    ILoggerFactory loggerFactory)
    {
        // Core service configuration
        services.Configure<ClustronConfig>(opts => {
            opts.ClusterId = config.ClusterId;
            opts.Version = config.Version;
            opts.Port = config.Port;
            opts.Roles = config.Roles;
            opts.RetryOptions = config.RetryOptions;
            opts.StaticNodes = config.StaticNodes;
            opts.UseDuplexConnections = config.UseDuplexConnections;
        });

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ClustronConfig>>().Value);
        services.AddSingleton(self);
        services.AddSingleton(loggerFactory);

        IClusterEventBus eventBus = new AsyncClusterEventBus(loggerFactory.CreateLogger<AsyncClusterEventBus>());
        services.AddSingleton<IClusterEventBus>(eventBus);

        var serializer = new JsonMessageSerializer();
        MessageBuilder.Configure(serializer);

        var discoveryProvider = new AdaptiveDiscoveryProvider(config.StaticNodes ?? []);
        var nullTransport = new NullTransport();
        var peerRegistry = new PeerRegistry(self);
        var peerManager = new ClusterPeerManager(peerRegistry, eventBus, loggerFactory.CreateLogger<ClusterPeerManager>());


        services.AddSingleton<IMessageSerializer>(serializer);
        services.AddSingleton<IDiscoveryProvider>(discoveryProvider);
        services.AddSingleton(peerRegistry);

        services.AddSingleton<ClusterPeerManager>();

        var context = new ClusterContext(self, nullTransport, discoveryProvider, peerManager, eventBus, config, loggerFactory);
        services.AddSingleton(context);
        services.AddSingleton<IClusterRuntime>(sp => sp.GetRequiredService<ClusterContext>());
        services.AddSingleton<IClusterLoggerProvider>(sp => sp.GetRequiredService<ClusterContext>());
        services.AddSingleton<IClusterCommunication>(sp => sp.GetRequiredService<ClusterContext>());
        services.AddSingleton<IClusterDiscovery>(sp => sp.GetRequiredService<ClusterContext>());
        services.AddSingleton<IElectionCoordinatorProvider>(sp => sp.GetRequiredService<ClusterContext>());


        services.AddSingleton<IElectionStrategy>(sp =>
                new BullyElectionStrategy(
                    sp.GetRequiredService<IClusterRuntime>(),
                    sp.GetRequiredService<IClusterLoggerProvider>(),
                    TimeSpan.FromSeconds(3)) // or bind this from config
        );


        services.AddSingleton<ElectionCoordinator>(sp =>
        {
            var coordinator = new ElectionCoordinator(self,
            sp.GetRequiredService<IElectionStrategy>(),
            sp.GetRequiredService<ILogger<ElectionCoordinator>>());

            context.SetElectionCoordinatory(coordinator);

            return coordinator;
        });

        services.AddSingleton(context);

        // Core metrics infrastructure
        services.AddSingleton<IMetricContributor, DefaultMessageStatsProvider>();
        services.AddSingleton<IMetricsSnapshotProvider>(sp => (IMetricsSnapshotProvider)sp.GetRequiredService<IMetricContributor>());


        // Transport and routing
        services.AddSingleton<ITransportFactory, TransportFactory>();
        services.AddSingleton<ITransport>(sp => sp.GetRequiredService<ITransportFactory>().Create());
        services.AddSingleton<IMessageRouter, ClusterMessageRouter>();

        services.AddSingleton<IHeartbeatMonitor>(sp =>
        {
            var context = sp.GetRequiredService<ClusterContext>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var metrics = sp.GetRequiredService<IMetricContributor>();

            var monitor = new TcpHeartbeatMonitor(
                                context,                         // IClusterRuntime
                                serializer,                      // IMessageSerializer
                                metrics,                           // IMessageStatsProvider
                                context                          // IClusterLoggerProvider
                            );

            var controller = new Lazy<ClusterNodeControllerBase>(() => sp.GetRequiredService<ClusterNodeControllerBase>());
            var transport = new Lazy<ITransport>(() => sp.GetRequiredService<ITransport>());

            monitor.SetClusterContext(controller, transport);
            return monitor;
        });


        services.AddSingleton<NodeJoinedHandler>(sp =>
            new NodeJoinedHandler(
                sp.GetRequiredService<ILogger<NodeJoinedHandler>>(),
                serializer,
                sp.GetRequiredService<ClusterPeerManager>()));

        services.AddSingleton<IHandshakeProtocol>(sp =>
                new TcpHandshakeProtocol(
                    context, context, context,
                    serializer,
                    new Lazy<IClusterState>(() => sp.GetRequiredService<IClusterState>()),
                    sp.GetRequiredService<NodeJoinedHandler>(),
                    context));


        services.AddSingleton<JoinManager>();
        services.AddSingleton<MessageDeduplicationCache>();


        services.AddSingleton<ILeaderElectionService, LeaderElectionService>();

        services.AddSingleton<HeartbeatMonitorBehavior>();
        services.AddSingleton<MetricsCollectorBehavior>();
        services.AddSingleton<ElectionBehavior>();

        services.AddSingleton<IRoleAwareBehavior, MetricsCollectorBehavior>();
        services.AddSingleton<IMetricsListener, MetricsCollectorBehavior>();

        services.AddSingleton<IRoleAwareBehavior, ElectionCoordinatorBehavior>();

        services.AddSingleton<ElectionCoordinatorBehavior>();
        services.AddSingleton<IRoleAwareBehavior, ClusterViewBroadcasterBehavior>();
        services.AddSingleton<ClusterViewBroadcasterBehavior>();

        services.AddSingleton<ClusterNodeControllerBase>(sp => {
            var roles = self.Roles ?? [];

            // Use Lazy to defer actual resolution until after DI builds the graph
            var behaviorFactories = new Dictionary<string, Func<IRoleAwareBehavior>>
            {
                ["HeartbeatMonitor"] = () => sp.GetRequiredService<HeartbeatMonitorBehavior>(),
                ["Election"] = () => sp.GetRequiredService<ElectionBehavior>(),
                ["MetricsCollector"] = () => sp.GetRequiredService<MetricsCollectorBehavior>(),
                ["ElectionCoordinator"] = () => sp.GetRequiredService<ElectionCoordinatorBehavior>(),
                ["ClusterViewBroadcaster"] = () => sp.GetRequiredService<ClusterViewBroadcasterBehavior>(),
            };

            var selectedBehaviors = behaviorFactories.Values
                .Select(factory => new Lazy<IRoleAwareBehavior>(factory))
                .ToList();

            return new RoleAwareClusterController(
                context,
                sp.GetRequiredService<JoinManager>(),
                context,
                sp.GetRequiredService<IHeartbeatMonitor>(),
                selectedBehaviors.Select(lazy => lazy.Value),
                context);
        });


        services.AddSingleton<IClusterState>(sp => sp.GetRequiredService<ClusterNodeControllerBase>());
        services.AddSingleton<IClusterStateMutator>(sp => sp.GetRequiredService<ClusterNodeControllerBase>());


        // Host and message handlers
        services.AddSingleton<ClustronNodeHost>();

        services.AddSingleton<IMessageHandler, MetricsSnapshotReceiverHandler>();
        services.AddSingleton<IMessageHandler, HandshakeRequestHandler>();
        services.AddSingleton<IMessageHandler, HandshakeResponseHandler>();
        services.AddSingleton<IMessageHandler, LeaderChangedHandler>();
        services.AddSingleton<IMessageHandler, HeartbeatHandler>();
        services.AddSingleton<IMessageHandler, HeartbeatSuspectHandler>();
        services.AddSingleton<IMessageHandler>(sp => sp.GetRequiredService<NodeJoinedHandler>());
        services.AddSingleton<IMessageHandler, NodeLeftHandler>(sp =>
            new NodeLeftHandler(
                sp.GetRequiredService<IHeartbeatMonitor>(),
                serializer,
                sp.GetRequiredService<ClusterPeerManager>(),
                sp.GetRequiredService<ILogger<NodeLeftHandler>>()));

        services.AddSingleton<IMessageHandler, MetricsRequestHandler>();
        services.AddSingleton<IMessageHandler, ClusterViewHandler>();

        return services;
    }

    public static IServiceCollection Register(
    this IServiceCollection services,
    ClustronConfig config,
    ILoggerFactory loggerFactory)
    {
        string ip = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
            .ToString() ?? "127.0.0.1";

        var self = new NodeInfo
        {
            ClusterId = config.ClusterId,
            Version = config.Version,
            Host = ip,
            Port = config.Port,
            NodeId = $"{config.ClusterId}-{ip.Replace(".", "_")}-{config.Port}",
            Roles = config.Roles 
        };

        if (config.StaticNodes != null)
        {
            foreach (var peer in config.StaticNodes)
            {
                peer.ClusterId = config.ClusterId;
                peer.Version = config.Version;
                peer.NodeId = $"{peer.ClusterId}-{peer.Host.Replace(".", "_")}-{peer.Port}";
            }
        }

        return services.AddClustronCoreServices(config, self, loggerFactory);
    }

}

