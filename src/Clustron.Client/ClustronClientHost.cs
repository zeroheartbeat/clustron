// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

namespace Clustron.Client;

internal class ClustronClientHost : IClustronClientHost
{
    public IClustronNodeManager NodeManager { get; }
    public IClustronClient Client { get; }

    public ClustronClientHost(IClustronNodeManager nodeManager, IClustronClient client)
    {
        NodeManager = nodeManager;
        Client = client;
    }
}

