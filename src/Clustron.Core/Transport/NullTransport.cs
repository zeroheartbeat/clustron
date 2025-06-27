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
using Clustron.Core.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;
public class NullTransport : ITransport
{
    public Task SendAsync(NodeInfo target, Message message)
    {
        return Task.CompletedTask;
    }

    public Task StartAsync(IMessageRouter router)
    {
        return Task.CompletedTask;
    }

    public Task<Message> WaitForResponseAsync(string expectedSenderId, string correlationID, TimeSpan timeout)
    {
        return Task.FromResult<Message>(null!); // Will crash if called — placeholder only
    }

    public Task BroadcastAsync(Message message, params string[] roles)
    {
        return Task.CompletedTask;
    }

    public void RemoveConnection(string nodeId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CanReachNodeAsync(NodeInfo node)
    {
        throw new NotImplementedException();
    }

    public Task HandlePeerDownAsync(string nodeId)
    {
        throw new NotImplementedException();
    }

    public Task SendImmediateAsync(NodeInfo target, Message message)
    {
        throw new NotImplementedException();
    }
}

