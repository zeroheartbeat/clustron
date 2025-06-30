// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using System.Collections.Concurrent;
using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Helpers;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Clustron.Core.Events;

public class AsyncClusterEventBus : IClusterEventBus
{
    private readonly ILogger<AsyncClusterEventBus> _logger;
    private readonly IMessageSerializer _serializer;
    private readonly Func<Func<IClusterEvent, Task>, IEventQueue<IClusterEvent>> _queueFactory;

    private IClusterCommunication _communication = null!;
    private NodeInfo _self = null!;

    private readonly ConcurrentDictionary<Type, List<Func<IClusterEvent, Task>>> _handlers = new();
    private readonly ConcurrentDictionary<string, Func<byte[], IClusterEvent>> _deserializers = new();
    private readonly ConcurrentDictionary<Func<IClusterEvent, Task>, IEventQueue<IClusterEvent>> _handlerQueues = new();

    public AsyncClusterEventBus(
        ILogger<AsyncClusterEventBus> logger,
        IMessageSerializer serializer,
        Func<Func<IClusterEvent, Task>, IEventQueue<IClusterEvent>>? queueFactory = null)
    {
        _logger = logger;
        _serializer = serializer;
        _queueFactory = queueFactory ?? (handler => new ChannelEventQueue<IClusterEvent>(handler, logger));
    }

    public void Configure(IClusterCommunication communication, NodeInfo self)
    {
        _communication = communication;
        _self = self;
    }

    public void Subscribe<T>(Func<T, Task> asyncHandler) where T : IClusterEvent
    {
        Func<IClusterEvent, Task> wrapper = e => asyncHandler((T)e);

        _handlers.AddOrUpdate(
            typeof(T),
            _ => new List<Func<IClusterEvent, Task>> { wrapper },
            (_, existing) => { lock (existing) existing.Add(wrapper); return existing; });

        _deserializers.TryAdd(typeof(T).AssemblyQualifiedName!, payload =>
            _serializer.Deserialize<T>(payload)!);

        var queue = _queueFactory(wrapper);
        queue.Start();

        _handlerQueues.TryAdd(wrapper, queue);
    }

    public void Subscribe<T>(Action<T> handler) where T : IClusterEvent
    {
        Subscribe<T>(e =>
        {
            handler(e);
            return Task.CompletedTask;
        });
    }

    public async Task PublishAsync(IClusterEvent evt, EventDispatchOptions? options = null, bool sendImmediate = false)
    {
        options ??= new EventDispatchOptions();
        var type = evt.GetType();

        if (options.Scope == DeliveryScope.ClusterWide)
            await BroadcastEventAsync(evt, type, sendImmediate);

        if (!_handlers.TryGetValue(type, out var handlerList))
            return;

        switch (options.Policy)
        {
            case DispatchPolicy.FireAndForget:
                foreach (var handler in handlerList.ToArray())
                    _ = Task.Run(() => handler(evt));
                break;

            case DispatchPolicy.Ordered:
                foreach (var handler in handlerList.ToArray())
                {
                    if (_handlerQueues.TryGetValue(handler, out var queue))
                        queue.Enqueue(evt);
                }
                break;

            case DispatchPolicy.Parallel:
                await Task.WhenAll(handlerList.ToArray().Select(h => h(evt)));
                break;

            case DispatchPolicy.Retry:
                foreach (var handler in handlerList.ToArray())
                {
                    await RetryHelper.RetryAsync(
                        () => handler(evt),
                        options.MaxRetryAttempts,
                        options.RetryDelayMilliseconds,
                        _logger);
                }
                break;
        }
    }

    public void Publish(IClusterEvent evt, bool sendImmediate = false)
        => PublishAsync(evt, sendImmediate:sendImmediate).GetAwaiter().GetResult();

    public async Task PublishFromNetworkAsync(byte[] payload, string eventType)
    {
        if (_deserializers.TryGetValue(eventType, out var factory))
        {
            var evt = factory(payload);
            await PublishAsync(evt, new EventDispatchOptions { Scope = DeliveryScope.LocalOnly });
        }
    }

    private async Task BroadcastEventAsync(IClusterEvent evt, Type type, bool sendImmediate = false)
    {
        var message = MessageBuilder.Create(
            _self.NodeId,
            evt.EventType,
            type.AssemblyQualifiedName!,
            evt);

        var targetRoles = evt.EventType == MessageTypes.CustomEvent
            ? new[] { ClustronRoles.Member }
            : Array.Empty<string>();

        await _communication.Transport.BroadcastAsync(message, sendImmediate, targetRoles);
    }
}
