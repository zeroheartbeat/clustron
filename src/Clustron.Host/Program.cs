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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
        config.AddCommandLine(args);
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();

        // Bind the full ClustronConfig once — includes JSON, env, CLI
        var clustronConfig = context.Configuration
            .GetSection("Clustron")
            .Get<ClustronConfig>() ?? throw new InvalidOperationException("Clustron configuration missing.");

        // Log roles for verification
        loggerFactory.CreateLogger("Startup").LogInformation("Loaded roles: {Roles}", string.Join(", ", clustronConfig.Roles ?? new List<string>()));

        // Register Clustron services with bound config
        services.Register(clustronConfig, loggerFactory);

        services.AddHostedService<ClustronStartupService>();
    })

    .Build();

await host.RunAsync();

