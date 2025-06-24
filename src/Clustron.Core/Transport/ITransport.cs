// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Messaging;
using Clustron.Core.Models;

namespace Clustron.Core.Transport
{
    public interface ITransport
    {
        Task SendAsync(NodeInfo target, Message message);
        Task StartAsync(IMessageRouter router);          
        Task<Message> WaitForResponseAsync(string expectedSenderId, string correlationId, TimeSpan timeout);
        Task BroadcastAsync(Message message, params string[] roles);
        void RemoveConnection(string nodeId);
        Task<bool> CanReachNodeAsync(NodeInfo node);
        Task HandlePeerDownAsync(string nodeId);
    }
}

