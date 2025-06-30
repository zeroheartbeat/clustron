// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Core.Cluster;
using Clustron.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Events
{
    public interface IClusterEventBus
    {
        void Publish(IClusterEvent evt, bool sendImmediate = false);

        Task PublishAsync(IClusterEvent evt, EventDispatchOptions? options = null, bool sendImmediate = false);
        void Subscribe<T>(Action<T> handler) where T : IClusterEvent;
        void Subscribe<T>(Func<T, Task> asyncHandler) where T : IClusterEvent;

        Task PublishFromNetworkAsync(byte[] payload, string eventType);

        void Configure(IClusterCommunication communication, NodeInfo self);
    }
}

