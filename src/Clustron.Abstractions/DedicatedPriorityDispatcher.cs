using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Clustron.Abstractions;

public class DedicatedPriorityDispatcher
{
    private readonly Channel<Func<Task>> _channel;

    public DedicatedPriorityDispatcher(int concurrency)
    {
        _channel = Channel.CreateUnbounded<Func<Task>>();

        for (int i = 0; i < concurrency; i++)
        {
            _ = Task.Run(ProcessLoop);
        }
    }

    public void Submit(Func<Task> work)
    {
        _channel.Writer.TryWrite(work);
    }

    private async Task ProcessLoop()
    {
        await foreach (var work in _channel.Reader.ReadAllAsync())
        {
            try { await work(); } catch { /* log if needed */ }
        }
    }
}

