// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

namespace Clustron.Core.Observability;

public class RollingCounter
{
    private readonly int[] _buffer;
    private readonly int _windowSize;
    private int _index = 0;
    private int _currentSecondCount = 0;
    private DateTime _lastSecond;
    private readonly object _lock = new();
    private long _totalSinceStart = 0;

    public RollingCounter(int windowSize)
    {
        _windowSize = windowSize;
        _buffer = new int[windowSize];
        _lastSecond = DateTime.UtcNow;
    }

    public void Increment()
    {
        lock (_lock)
        {
            Flush();
            _currentSecondCount++;
            _totalSinceStart++;
        }
    }

    private void Flush()
    {
        var now = DateTime.UtcNow;
        int elapsedSeconds = (int)(now - _lastSecond).TotalSeconds;

        if (elapsedSeconds <= 0)
            return;

        for (int i = 0; i < elapsedSeconds; i++)
        {
            _index = (_index + 1) % _windowSize;
            _buffer[_index] = 0;
        }
        _buffer[_index] = _currentSecondCount;

        _lastSecond = _lastSecond.AddSeconds(elapsedSeconds);
        _currentSecondCount = 0;
    }

    public int[] GetPerSecondRates(int seconds)
    {
        lock (_lock)
        {
            Flush();

            if (seconds <= 0 || seconds > _windowSize)
                throw new ArgumentOutOfRangeException(nameof(seconds), $"Must be between 1 and {_windowSize}");

            // Return the most recent `seconds` values (oldest to newest)
            return Enumerable.Range(_windowSize - seconds, seconds)
                .Select(i => _buffer[(_index + 1 + i) % _windowSize])
                .ToArray();
        }
    }

    public long Total()
    {
        lock (_lock)
        {
            return _totalSinceStart;
        }
    }
}


