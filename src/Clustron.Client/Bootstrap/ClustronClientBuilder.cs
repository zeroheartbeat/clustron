// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Client;
using Clustron.Client.Handlers;
using Clustron.Core.Bootstrap;
using Clustron.Core.Client;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Hosting;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
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

    /// <summary>
    /// Initializes the builder from a configuration section (e.g., appsettings.json).
    /// </summary>
    public static ClustronClientBuilder FromConfiguration(IConfigurationSection section)
    {
        var config = new ClustronConfig();
        section.Bind(config);

        return new ClustronClientBuilder { _config = config };
    }

    /// <summary>
    /// Sets logging config section (usually root IConfiguration) to pull Logging section from.
    /// </summary>
    public ClustronClientBuilder WithLogging(IConfiguration config)
    {
        _loggingConfig = config;
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

        // Step 1: Create bootstrapper using prebuilt services
        var bootstrapper = new ClustronBootstrapper();
        bootstrapper.UseServices(services); 

        // Step 2: Start cluster (will now use our logger factory)
        bootstrapper.Start(_args ?? Array.Empty<string>());

        // Step 3: Everything else stays the same
        var resolved = bootstrapper.Services;

        var config = _config ?? resolved.GetRequiredService<ClustronConfig>();
        var controller = resolved.GetRequiredService<ClusterNodeControllerBase>();
        var serializer = resolved.GetRequiredService<IMessageSerializer>();

        var clientCore = new ClustronClientCore(controller, serializer);
        var client = new ClustronClient(clientCore);

        var handler = new ClientMessageHandler(
            client,
            resolved.GetRequiredService<ILogger<ClientMessageHandler>>());

        var router = resolved.GetRequiredService<IMessageRouter>();
        router?.AddHandler(handler);

        var nodeManager = new ClustronNodeManager(bootstrapper, config);
        return new ClustronClientHost(nodeManager, client);
    }


}

