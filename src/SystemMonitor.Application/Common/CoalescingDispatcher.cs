using System;
using System.Threading;

namespace SystemMonitor.Application.Common;

/// <summary>
/// Coalesces a burst of values into one delayed apply. Uses a single reused
/// timer instead of constructing a new Timer (and its native handle) on every
/// Post call.
/// </summary>
public sealed class CoalescingDispatcher<T> : IDisposable
{
    private readonly TimeSpan _quietWindow;
    private readonly TimeSpan _maxWait;
    private readonly Action<T> _apply;
    private readonly object _gate = new();
    private readonly Timer _timer;

    private T? _pending;
    private bool _hasPending;
    private DateTime _firstPendingAtUtc;
    private bool _disposed;

    public CoalescingDispatcher(Action<T> apply, TimeSpan? quietWindow = null, TimeSpan? maxWait = null)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _quietWindow = quietWindow ?? TimeSpan.FromMilliseconds(150);
        _maxWait = maxWait ?? TimeSpan.FromMilliseconds(500);
        _timer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Post(T value)
    {
        lock (_gate)
        {
            if (_disposed) return;

            var now = DateTime.UtcNow;
            if (!_hasPending)
                _firstPendingAtUtc = now;

            _pending = value;
            _hasPending = true;

            var sinceFirst = now - _firstPendingAtUtc;
            var delay = sinceFirst >= _maxWait ? TimeSpan.Zero : _quietWindow;
            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        T value;
        lock (_gate)
        {
            if (_disposed || !_hasPending) return;
            value = _pending!;
            _hasPending = false;
            _pending = default;
        }

        _apply(value);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }
}
