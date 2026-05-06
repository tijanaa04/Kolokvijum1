namespace Kolokvijum_1;

/// <summary>
/// Thread-safe priority queue where lower Priority number = higher priority (dequeued first).
/// </summary>
public class ThreadSafePriorityQueue
{
    private readonly SortedList<(int Priority, DateTime Enqueued), Job> _queue = new();
    private readonly object _lock = new();
    private readonly int _maxSize;

    public ThreadSafePriorityQueue(int maxSize)
    {
        _maxSize = maxSize;
    }

    public bool TryEnqueue(Job job)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxSize)
                return false;

            // Use DateTime as tiebreaker to preserve insertion order within same priority
            var key = (job.Priority, DateTime.UtcNow);
            _queue[key] = job;
            return true;
        }
    }

    public bool TryDequeue(out Job? job)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                job = null;
                return false;
            }

            var firstKey = _queue.Keys[0];
            job = _queue[firstKey];
            _queue.RemoveAt(0);
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (_lock) return _queue.Count;
        }
    }

    public IEnumerable<Job> GetTopN(int n)
    {
        lock (_lock)
        {
            return _queue.Values.Take(n).ToList();
        }
    }

    public bool Contains(Guid id)
    {
        lock (_lock)
        {
            return _queue.Values.Any(j => j.Id == id);
        }
    }
}
