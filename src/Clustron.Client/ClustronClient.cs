// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Client.Communication;
using Clustron.Client.Management;
using Clustron.Client.Models;
using Clustron.Client.Monitoring;
using Clustron.Core.Client;
using Clustron.Core.Messaging;
using Clustron.Core.Models;

namespace Clustron.Client;

public class ClustronClient : IClustronClient
{
    public IClusterManager Management { get; }
    public IMessenger Messaging { get; }
    public IMonitor Monitoring { get; }

    public ClustronClient(ClustronClientCore core)
    {
        Management = new ClusterManager(core);
        Messaging = new ClusterMessenger(core);
        Monitoring = new ClusterMonitor(core);
    }
}

