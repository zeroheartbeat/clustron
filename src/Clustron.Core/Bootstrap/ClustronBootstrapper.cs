// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Configuration;
using Clustron.Core.Extensions;
using Clustron.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Clustron.Core.Bootstrap;

public class ClustronBootstrapper
{
    private readonly IServiceCollection _services = new ServiceCollection();
    private IServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;
    private IServiceCollection? _prebuiltServices;

    public IServiceProvider Services => _serviceProvider
        ?? throw new InvalidOperationException("Bootstrapper not started yet");

    public IConfiguration Configuration => _configuration
        ?? throw new InvalidOperationException("Configuration not built");

    /// <summary>
    /// Allows external service registrations before starting.
    /// </summary>
    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_services);
    }

    public void Start(string[]? args = null)
    {
        // Build configuration from JSON, env, CLI
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        if (args != null)
            configBuilder.AddCommandLine(args);

        _configuration = configBuilder.Build();

        _services.AddLogging(logging =>
        {
            logging.AddConfiguration(_configuration.GetSection("Logging")); 
            logging.AddConsole();                                           
        });

        // Build a logger factory from the service collection
        var tmpProvider = _services.BuildServiceProvider();
        var loggerFactory = tmpProvider.GetRequiredService<ILoggerFactory>();

        _services.AddSingleton(loggerFactory); 


        // Respect externally-injected ClustronConfig
        if (!_services.Any(s => s.ServiceType == typeof(ClustronConfig)))
        {
            var clustronConfig = _configuration.GetSection("Clustron").Get<ClustronConfig>()
                ?? throw new InvalidOperationException("Missing Clustron config");

            _services.AddSingleton(clustronConfig);
        }

        // Finalize DI
        _serviceProvider = _services.BuildServiceProvider();

        // Register core services using config + logger
        var config = _serviceProvider.GetRequiredService<ClustronConfig>();
        _services.Register(config, loggerFactory);

        // Build final provider with everything
        _serviceProvider = _services.BuildServiceProvider();

        var nodeHost = _serviceProvider.GetRequiredService<ClustronNodeHost>();
        nodeHost.StartAsync().GetAwaiter().GetResult();
    }

    public void UseServices(IServiceCollection services)    
    {
        _prebuiltServices = services;
    }

}
