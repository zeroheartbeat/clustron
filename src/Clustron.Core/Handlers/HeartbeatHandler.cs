// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Health;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace Clustron.Core.Handlers;
public class HeartbeatHandler : IMessageHandler
{
    private readonly IHeartbeatMonitor _monitor;
    private readonly ILogger<HeartbeatHandler> _logger;
    private readonly IMessageSerializer _serializer;


    public string Type => MessageTypes.Heartbeat;

    public HeartbeatHandler(IHeartbeatMonitor monitor, IClusterLoggerProvider loggerProvider, IMessageSerializer serializer)
    {
        _monitor = monitor;
        _logger = loggerProvider.GetLogger<HeartbeatHandler>();
        _serializer = serializer;

    }

    public Task HandleAsync(Message message)
    {
        _logger.LogDebug("Received heartbeat from {SenderId}", message.SenderId);
        _monitor.MarkHeartbeatReceived(message.SenderId);
        return Task.CompletedTask;
    }
}

