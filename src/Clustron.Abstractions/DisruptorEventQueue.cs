// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;

namespace Clustron.Abstractions
{
 

    public class DisruptorEventQueue<T> : IEventQueue<T>
    {
        private class Wrapper { public T? Item; }

        private readonly Disruptor<Wrapper> _disruptor;
        private readonly RingBuffer<Wrapper> _ringBuffer;

        public DisruptorEventQueue(Action<T> handler, int size = 1024)
        {
            _disruptor = new Disruptor<Wrapper>(() => new Wrapper(), size,
                TaskScheduler.Default, ProducerType.Multi, new BlockingWaitStrategy());

            _disruptor.HandleEventsWith(new Handler(handler));
            _ringBuffer = _disruptor.Start();
        }

        public void Enqueue(T item)
        {
            long seq = _ringBuffer.Next();
            _ringBuffer[seq].Item = item;
            _ringBuffer.Publish(seq);
        }

        public void Start() { /* Already started in constructor */ }

        public void Stop() => _disruptor.Shutdown();

        private class Handler : IEventHandler<Wrapper>
        {
            private readonly Action<T> _handler;
            public Handler(Action<T> handler) => _handler = handler;

            public void OnEvent(Wrapper evt, long sequence, bool endOfBatch)
            {
                if (evt.Item is { } item)
                    _handler(item);
            }
        }
    }

}
