using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Clustron.Core.Transport;

public class PriorityTransportQueue
{
    private readonly Channel<PrioritizedMessage> _high = Channel.CreateUnbounded<PrioritizedMessage>();
    private readonly Channel<PrioritizedMessage> _medium = Channel.CreateUnbounded<PrioritizedMessage>();
    private readonly Channel<PrioritizedMessage> _low = Channel.CreateBounded<PrioritizedMessage>(100);

    private readonly Func<PrioritizedMessage, Task> _handler;
    private readonly CancellationTokenSource _cts = new();

    public PriorityTransportQueue(Func<PrioritizedMessage, Task> handler)
    {
        _handler = handler;
        Task.Run(ProcessLoop);
    }

    public async Task EnqueueAsync(PrioritizedMessage msg)
    {
        var channel = msg.Priority switch
        {
            MessagePriority.High => _high,
            MessagePriority.Medium => _medium,
            MessagePriority.Low => _low
        };

        if (!channel.Writer.TryWrite(msg))
        {
            await channel.Writer.WriteAsync(msg); // Wait if necessary
        }
    }

    private async Task ProcessLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_high.Reader.TryRead(out var h)) { await _handler(h); continue; }
            if (_medium.Reader.TryRead(out var m)) { await _handler(m); continue; }
            if (_low.Reader.TryRead(out var l)) { await _handler(l); continue; }

            await Task.Delay(1);
        }
    }

    public void Stop() => _cts.Cancel();
}

