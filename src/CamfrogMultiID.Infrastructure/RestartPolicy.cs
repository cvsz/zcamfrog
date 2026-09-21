namespace CamfrogMultiID.Infrastructure;

/// <summary>
/// Bounds automatic restarts so a crashing client cannot spin forever.
/// Allows up to <see cref="MaxRestarts"/> restarts per <see cref="Window"/>.
/// Pure logic over timestamps; the caller supplies storage.
/// </summary>
public sealed class RestartPolicy
{
    public static int MaxRestarts { get; } = 3;
    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(10);

    private readonly Dictionary<long, Queue<DateTime>> _attempts = new();
    private readonly object _gate = new();

    public bool ShouldRestart(long accountId, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        lock (_gate)
        {
            if (!_attempts.TryGetValue(accountId, out var queue))
            {
                queue = new Queue<DateTime>();
                _attempts[accountId] = queue;
            }

            while (queue.Count > 0 && (nowUtc - queue.Peek()) > Window)
                queue.Dequeue();

            if (queue.Count >= MaxRestarts)
                return false;

            queue.Enqueue(nowUtc);
            return true;
        }
    }

    public int GetAttemptCount(long accountId, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (!_attempts.TryGetValue(accountId, out var queue))
                return 0;
            while (queue.Count > 0 && (nowUtc - queue.Peek()) > Window)
                queue.Dequeue();
            return queue.Count;
        }
    }

    public void Reset(long accountId)
    {
        lock (_gate)
        {
            _attempts.Remove(accountId);
        }
    }
}
