// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Clustron.Abstractions;
using Clustron.Core.Cluster;
using Clustron.Core.Configuration;
using Clustron.Core.Helpers;
using Clustron.Core.Messaging;
using Clustron.Core.Models;
using Clustron.Core.Serialization;
using Clustron.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Clustron.Core.Events
{
    public class AsyncClusterEventBus : IClusterEventBus
    {
        private readonly ILogger<AsyncClusterEventBus> _logger;
        private readonly IMessageSerializer _serializer;
        private IClusterCommunication _communication;
        private NodeInfo _self;

        private readonly ConcurrentDictionary<Type, List<Func<IClusterEvent, Task>>> _asyncHandlers = new();
        private readonly ConcurrentDictionary<string, Func<byte[], IClusterEvent>> _deserializers = new();

        private readonly Channel<IClusterEvent> _localEventQueue =
            Channel.CreateBounded<IClusterEvent>(new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        public AsyncClusterEventBus(
            ILogger<AsyncClusterEventBus> logger,
            IMessageSerializer serializer)
        {
            _logger = logger;
            _serializer = serializer;
            _ = Task.Run(ProcessEventQueueAsync);
        }

        public void Configure(IClusterCommunication communication, NodeInfo self)
        {
            _communication = communication;
            _self = self;
        }

        public void Subscribe<T>(Action<T> handler) where T : IClusterEvent
        {
            Func<IClusterEvent, Task> wrapper = e =>
            {
                handler((T)e);
                return Task.CompletedTask;
            };

            AddHandler(typeof(T), wrapper);

            _deserializers.TryAdd(typeof(T).AssemblyQualifiedName!, payload =>
                    _serializer.Deserialize<T>(payload)!);
        }

        public void Subscribe<T>(Func<T, Task> asyncHandler) where T : IClusterEvent
        {
            Func<IClusterEvent, Task> wrapper = e => asyncHandler((T)e);
            AddHandler(typeof(T), wrapper);

            _deserializers.TryAdd(typeof(T).AssemblyQualifiedName!, payload =>
                    _serializer.Deserialize<T>(payload)!);
        }

        private void AddHandler(Type type, Func<IClusterEvent, Task> handler)
        {
            _asyncHandlers.AddOrUpdate(
                type,
                _ => new List<Func<IClusterEvent, Task>> { handler },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(handler);
                    }
                    return list;
                });
        }

        public async Task PublishAsync(IClusterEvent evt, EventDispatchOptions? options = null)
        {
            options ??= new EventDispatchOptions();
            var type = evt.GetType();

            if (options.Scope == DeliveryScope.ClusterWide)
            {
                await BroadcastEventAsync(evt, type);
            }

            if (!_asyncHandlers.TryGetValue(type, out var handlers))
                return;

            if (options.Scope == DeliveryScope.LocalOnly && options.Policy == DispatchPolicy.FireAndForget)
            {
                await _localEventQueue.Writer.WriteAsync(evt);
                return;
            }

            switch (options.Policy)
            {
                case DispatchPolicy.Parallel:
                    await Task.WhenAll(handlers.ToArray().Select(h => h(evt)));
                    break;

                case DispatchPolicy.Ordered:
                    foreach (var handler in handlers.ToArray())
                        await handler(evt);
                    break;

                case DispatchPolicy.Retry:
                    foreach (var handler in handlers.ToArray())
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

        public void Publish(IClusterEvent evt) =>
            PublishAsync(evt).GetAwaiter().GetResult();

        public Task PublishFromNetworkAsync(byte[] payload, string eventType)
        {
            if (_deserializers.TryGetValue(eventType, out var factory))
            {
                var evt = factory(payload);
                return PublishAsync(evt, new EventDispatchOptions() { Scope = DeliveryScope.LocalOnly });
            }

            return Task.CompletedTask;
        }

        private async Task BroadcastEventAsync(IClusterEvent evt, Type type)
        {
            var message = MessageBuilder.Create(
                _self.NodeId,
                evt.EventType,
                type.AssemblyQualifiedName!,
                evt);

            var targetRoles = evt.EventType == MessageTypes.CustomEvent
                ? new[] { ClustronRoles.Member }
                : Array.Empty<string>();

            await _communication.Transport.BroadcastAsync(message, targetRoles);
        }

        private async Task ProcessEventQueueAsync()
        {
            await foreach (var evt in _localEventQueue.Reader.ReadAllAsync())
            {
                var type = evt.GetType();

                if (_asyncHandlers.TryGetValue(type, out var handlers))
                {
                    foreach (var handler in handlers.ToArray())
                    {
                        try
                        {
                            await handler(evt);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Event handler failed for {Type}", type.Name);
                        }
                    }
                }
            }
        }
    }
}
