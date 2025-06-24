// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Abstractions;
using Clustron.Core.Messaging;
using Clustron.Core.Observability;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Clustron.Core.Messaging;

public class ClusterMessageRouter : IMessageRouter
{
    private readonly ConcurrentDictionary<string, IMessageHandler> _handlerMap = new();
    private readonly IMetricContributor _metrics;
    private readonly ILogger<ClusterMessageRouter> _logger;

    public ClusterMessageRouter(IEnumerable<IMessageHandler> handlers, IMetricContributor metrics, ILogger<ClusterMessageRouter> logger)
    {
        _metrics = metrics;
        _logger = logger;

        foreach (var handler in handlers)
        {
            if (_handlerMap.TryAdd(handler.Type, handler))
            {
                _logger.LogDebug("Registered handler for {MessageType}: {HandlerType}", handler.Type, handler.GetType().FullName);
            }
            else
            {
                _logger.LogWarning("Duplicate handler detected for message type {MessageType}", handler.Type);
            }
        }

        _logger.LogDebug("ClusterMessageRouter initialized with {Count} message handlers.", _handlerMap.Count);
    }

    public Task AddHandler(IMessageHandler handler)
    {
        if (_handlerMap.TryAdd(handler.Type, handler))
        {
            _logger.LogInformation("Dynamically added handler for {MessageType}: {HandlerType}",
                handler.Type, handler.GetType().FullName);
        }
        else
        {
            _logger.LogWarning("Handler already exists for message type: {MessageType}", handler.Type);
        }

        return Task.CompletedTask;
    }

    public async Task RouteAsync(Message message)
    {
        if (string.IsNullOrWhiteSpace(message.TypeInfo))
        {
            _logger.LogError("Cannot route message with null TypeInfo. CorrelationId={CorrelationId}, Type={Type}", message.CorrelationId, message.TypeInfo);
            return;
        }

        if (_handlerMap.TryGetValue(message.MessageType, out var handler))
        {
            try
            {
                _metrics.Increment(MetricKeys.Messages.Received);
                await handler.HandleAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling message of type {Type} from {Sender}", message.MessageType, message.SenderId);
            }
        }
        else
        {
            _logger.LogWarning("No handler found for message type: {Type}", message.MessageType);
        }
    }
}

