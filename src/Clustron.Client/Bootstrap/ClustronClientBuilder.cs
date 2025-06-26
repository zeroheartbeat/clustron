// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Client;
using Clustron.Client.Handlers;
using Clustron.Core.Bootstrap;
using Clustron.Core.Client;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Handlers;
using Clustron.Core.Hosting;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Observability;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clustron.Client.Bootstrap;

/// <summary>
/// Modified version of ClustronClientBuilder that supports injecting logging config.
/// </summary>
public class ClustronClientBuilder
{
    private ClustronConfig? _config;
    private string[]? _args;
    private IConfiguration? _loggingConfig;
    private IConfiguration? _fullConfig;

    /// <summary>
    /// Initializes the builder from a configuration section (e.g., appsettings.json).
    /// </summary>
    //public static ClustronClientBuilder FromConfiguration(IConfigurationSection section)
    //{
    //    var config = new ClustronConfig();
    //    section.Bind(config);

    //    return new ClustronClientBuilder { _config = config };
    //}
    public static ClustronClientBuilder FromConfiguration(IConfiguration section)
    {
        var builder = new ClustronClientBuilder();
        builder._config = section.Get<ClustronConfig>();
        builder._fullConfig = section as IConfiguration ?? throw new InvalidOperationException("Invalid configuration section");
        return builder;
    }

    public static ClustronClientBuilder FromConfiguration(IConfigurationSection section, IConfiguration fullConfig)
    {
        var builder = new ClustronClientBuilder();
        builder._config = section.Get<ClustronConfig>();
        builder._fullConfig = fullConfig;
        return builder;
    }

    /// <summary>
    /// Sets logging config section (usually root IConfiguration) to pull Logging section from.
    /// </summary>
    //public ClustronClientBuilder WithLogging(IConfiguration config)
    //{
    //    _loggingConfig = config;
    //    return this;
    //}
    public ClustronClientBuilder WithLogging(IConfiguration loggingConfig)
    {
        _loggingConfig = loggingConfig;
        return this;
    }

    public ClustronClientBuilder WithOverrides(Action<ClustronConfig> overrideAction)
    {
        if (_config == null)
            _config = new ClustronConfig();

        overrideAction(_config);
        return this;
    }

    public ClustronClientBuilder WithCommandLineArgs(string[] args)
    {
        _args = args;
        return this;
    }

    public IClustronClientHost Build()
    {
        // Step 0: Build your own service collection with logging FIRST
        var services = new ServiceCollection();

        // Step 1: Register config and logging if provided
        if (_config != null)
            services.AddSingleton(_config);

        if (_loggingConfig != null)
        {
            var loggingSection = _loggingConfig.GetSection("Logging");
            services.AddLogging(logging =>
            {
                logging.AddConfiguration(loggingSection);
                logging.AddConsole();
            });
        }

        var bootstrapper = new ClustronBootstrapper();
        bootstrapper.UseServices(services);

        if (_fullConfig != null)
            bootstrapper.UseConfiguration(_fullConfig);

        bootstrapper.Start(_args ?? Array.Empty<string>());

        // Step 3: Everything else stays the same
        var resolved = bootstrapper.Services;

        var config = _config ?? resolved.GetRequiredService<ClustronConfig>();
        var controller = resolved.GetRequiredService<ClusterNodeControllerBase>();
        var serializer = resolved.GetRequiredService<IMessageSerializer>();
        var metricsContributor = resolved.GetRequiredService<IMetricContributor>();

        var clientCore = new ClustronClientCore(controller, metricsContributor, serializer);
        var client = new ClustronClient(clientCore);

        var handler = new ClientMessageHandler(
            client,
            resolved.GetRequiredService<ILogger<ClientMessageHandler>>());

        var router = resolved.GetRequiredService<IMessageRouter>();
        router?.AddHandler(handler);

        var eventHandler = new ClusterEventMessageHandler(controller.Runtime.EventBus, serializer, controller.Runtime.Self.NodeId);
        router?.AddHandler(eventHandler);

        var nodeManager = new ClustronNodeManager(bootstrapper, config);
        return new ClustronClientHost(nodeManager, client);
    }


}

