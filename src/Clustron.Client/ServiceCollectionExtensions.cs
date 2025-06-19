// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Client;
using Clustron.Client.Bootstrap;
using Clustron.Client.Models;
using Clustron.Core.Client;
using Clustron.Core.Cluster;
using Clustron.Core.Messaging;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clustron.Client;

/// <summary>
/// Provides extension methods to register the Clustron client into a DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Clustron client and its dependencies into the service collection.
    /// Ensure ClustronCore has already been configured.
    /// </summary>
    public static IServiceCollection AddClustronClient(this IServiceCollection services)
    {
        // Register ClustronClientCore using required services from Core
        services.TryAddSingleton<ClustronClientCore>(sp =>
        {
            var controller = sp.GetRequiredService<ClusterNodeControllerBase>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new ClustronClientCore(controller, serializer);
        });

        // Register the main public-facing interface
        services.TryAddSingleton<IClustronClient>(sp =>
        {
            var core = sp.GetRequiredService<ClustronClientCore>();
            return new ClustronClient(core);
        });

        return services;
    }

    public static IServiceCollection AddClustronClient(this IServiceCollection services, IConfigurationSection configSection)
    {
        var builder = ClustronClientBuilder.FromConfiguration(configSection);
        var host = builder.Build();

        services.AddSingleton<IClustronClientHost>(host);
        services.AddSingleton<IClustronClient>(host.Client);
        services.AddSingleton<IClustronNodeManager>(host.NodeManager);

        return services;
    }
}

