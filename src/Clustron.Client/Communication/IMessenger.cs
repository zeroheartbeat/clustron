// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Client.Models;
using Clustron.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Communication
{
    public interface IMessenger
    {
        Task SendAsync<T>(string nodeId, T payload);
        Task BroadcastAsync<T>(T payload);
        void OnMessageReceived<T>(Func<T, string, Task> handler);

        public Task PublishAsync<T>(T @event, EventDispatchOptions? options = null)
                                            where T : IClusterEvent;

        void Subscribe<T>(Func<T, Task> handler) where T : IClusterEvent;

        void Subscribe<TEvent, TPayload>(Func<TEvent, Task> handler)
    where TEvent : CustomClusterEvent<TPayload>, new();

    }
}
