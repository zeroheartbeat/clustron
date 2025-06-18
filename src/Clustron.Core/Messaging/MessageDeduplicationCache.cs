// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Messaging
{
    public class MessageDeduplicationCache
    {
        private readonly ConcurrentDictionary<Guid, DateTime> _seen = new();
        private readonly TimeSpan _window;
        private readonly Timer _cleanupTimer;

        public MessageDeduplicationCache(TimeSpan? window = null)
        {
            _window = window ?? TimeSpan.FromMinutes(2);
            _cleanupTimer = new Timer(Cleanup, null, _window, _window);
        }

        public bool IsDuplicate(Guid id)
        {
            var now = DateTime.UtcNow;
            return !_seen.TryAdd(id, now);
        }

        private void Cleanup(object? _)
        {
            var threshold = DateTime.UtcNow - _window;
            foreach (var kvp in _seen)
            {
                if (kvp.Value < threshold)
                    _seen.TryRemove(kvp.Key, out var _);
            }
        }
    }

}

