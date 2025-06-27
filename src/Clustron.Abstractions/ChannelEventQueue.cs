// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Clustron.Abstractions
{
    public class ChannelEventQueue<T> : IEventQueue<T>
    {
        private readonly Channel<T> _channel;
        private readonly Func<T, Task> _handler;
        private readonly ILogger _logger;
        private CancellationTokenSource _cts = new();

        public ChannelEventQueue(Func<T, Task> handler, ILogger logger)
        {
            _handler = handler;
            _logger = logger;
            _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Start()
        {
            _ = Task.Run(async () =>
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    try { await _handler(item); }
                    catch (Exception ex) { _logger.LogError(ex, "Handler failure for {Type}", typeof(T).Name); }
                }
            });
        }

        public void Enqueue(T item)
        {
            _channel.Writer.TryWrite(item);
        }

        public void Stop()
        {
            _cts.Cancel();
        }
    }

}
