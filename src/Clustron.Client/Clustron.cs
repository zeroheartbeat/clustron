// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Client.Bootstrap;
using Clustron.Client.Handlers;
using Clustron.Core.Bootstrap;
using Clustron.Core.Client;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client
{
    public static class Clustron
    {
        private static IClustronClient? _client;
        private static IClustronClientHost? _host;

        /// <summary>
        /// Initialize Clustron using the default configuration from appsettings.json and a clusterId.
        /// </summary>
        public static IClustronClient Initialize(string clusterId)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return Initialize(config, clusterId);
        }

        /// <summary>
        /// Initialize Clustron using a custom IConfiguration and clusterId.
        /// </summary>
        public static IClustronClient Initialize(IConfiguration config, string clusterId)
        {
            var section = config.GetSection("Clustron");
            var boundConfig = new ClustronConfig();
            section.Bind(boundConfig);

            if (!string.Equals(boundConfig.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"ClusterId '{clusterId}' does not match config (found '{boundConfig.ClusterId ?? "null"}').");
            }

            var builder = ClustronClientBuilder
                .FromConfiguration(section)
                .WithLogging(config);

            _host = builder.Build();
            _client = _host.Client;

            return _client;
        }

        public static IClustronClient Client =>
            _client ?? throw new InvalidOperationException("Clustron is not initialized.");

        public static void Shutdown()
        {
            _host?.NodeManager.StopAsync().Wait();
            _host = null;
            _client = null;
        }
    }


}

