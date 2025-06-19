// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Bootstrap;
using Clustron.Core.Configuration;
using Clustron.Core.Hosting;

namespace Clustron.Client;

internal class ClustronNodeManager : IClustronNodeManager
{
    private readonly ClustronBootstrapper _bootstrapper;
    private readonly ClustronConfig _config;
    private bool _isRunning;

    public ClustronNodeManager(ClustronBootstrapper bootstrapper, ClustronConfig config)
    {
        _bootstrapper = bootstrapper;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _bootstrapper.Start();
        _isRunning = true;
        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = false;
        return Task.CompletedTask;
    }

    public bool IsRunning => _isRunning;
    public ClustronConfig Configuration => _config;
    public string NodeId => _config.ClusterId ?? "unknown";
}

