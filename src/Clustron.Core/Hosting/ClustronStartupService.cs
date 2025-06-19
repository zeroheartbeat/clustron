// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Clustron.Core.Hosting;

public class ClustronStartupService : IHostedService
{
    private readonly ClustronNodeHost _nodeHost;

    public ClustronStartupService(ClustronNodeHost nodeHost)
    {
        _nodeHost = nodeHost;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _nodeHost.StartAsync(); // this should initialize the node
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

