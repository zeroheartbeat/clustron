// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Core.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Events
{
    public class AsyncClusterEventBus : IClusterEventBus
    {
        private readonly ILogger<AsyncClusterEventBus> _logger;
        private readonly ConcurrentDictionary<Type, List<Func<IClusterEvent, Task>>> _asyncHandlers = new();

        public AsyncClusterEventBus(ILogger<AsyncClusterEventBus> logger)
        { 
            _logger = logger;
        }

        public void Subscribe<T>(Action<T> handler) where T : IClusterEvent
        {
            Func<IClusterEvent, Task> wrapper = e =>
            {
                handler((T)e); // Cast + call
                return Task.CompletedTask;
            };

            AddHandler(typeof(T), wrapper);
        }

        public void Subscribe<T>(Func<T, Task> asyncHandler) where T : IClusterEvent
        {
            Func<IClusterEvent, Task> wrapper = e => asyncHandler((T)e); // Cast + await
            AddHandler(typeof(T), wrapper);
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
            var type = evt.GetType();
            if (!_asyncHandlers.TryGetValue(type, out var handlers)) return;

            var opts = options ?? new EventDispatchOptions();

            switch (opts.Policy)
            {
                case DispatchPolicy.FireAndForget:
                    foreach (var handler in handlers.ToArray())
                        _ = Task.Run(() => handler(evt)); // Don't await
                    break;

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
                            opts.MaxRetryAttempts,
                            opts.RetryDelayMilliseconds,
                            _logger);
                    }
                    break;
            }
        }


        public void Publish(IClusterEvent evt) =>
            PublishAsync(evt).GetAwaiter().GetResult();
    }

}

