namespace Kolokvijum_1.Tests;

public class ThreadSafePriorityQueueTests
{
    private static Job MakeJob(int priority) => new Job
    {
        Id = Guid.NewGuid(),
        Type = JobType.IO,
        Payload = "delay:10",
        Priority = priority
    };

    [Fact]
    public void Enqueue_WithinLimit_ReturnsTrue()
    {
        var queue = new ThreadSafePriorityQueue(5);
        Assert.True(queue.TryEnqueue(MakeJob(1)));
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Enqueue_AtLimit_ReturnsFalse()
    {
        var queue = new ThreadSafePriorityQueue(2);
        queue.TryEnqueue(MakeJob(1));
        queue.TryEnqueue(MakeJob(2));
        Assert.False(queue.TryEnqueue(MakeJob(3)));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new ThreadSafePriorityQueue(5);
        Assert.False(queue.TryDequeue(out var job));
        Assert.Null(job);
    }

    [Fact]
    public void Dequeue_ReturnsHighestPriorityFirst()
    {
        var queue = new ThreadSafePriorityQueue(10);
        queue.TryEnqueue(MakeJob(3));
        queue.TryEnqueue(MakeJob(1));
        queue.TryEnqueue(MakeJob(2));

        queue.TryDequeue(out var first);
        queue.TryDequeue(out var second);
        queue.TryDequeue(out var third);

        Assert.Equal(1, first!.Priority);
        Assert.Equal(2, second!.Priority);
        Assert.Equal(3, third!.Priority);
    }

    [Fact]
    public void GetTopN_ReturnsCorrectCount()
    {
        var queue = new ThreadSafePriorityQueue(10);
        for (int i = 5; i >= 1; i--)
            queue.TryEnqueue(MakeJob(i));

        var top = queue.GetTopN(3).ToList();
        Assert.Equal(3, top.Count);
        Assert.Equal(1, top[0].Priority);
        Assert.Equal(2, top[1].Priority);
    }

    [Fact]
    public void GetTopN_LargerThanQueue_ReturnsAll()
    {
        var queue = new ThreadSafePriorityQueue(5);
        queue.TryEnqueue(MakeJob(1));
        queue.TryEnqueue(MakeJob(2));

        var top = queue.GetTopN(100).ToList();
        Assert.Equal(2, top.Count);
    }

    [Fact]
    public void Contains_ExistingId_ReturnsTrue()
    {
        var queue = new ThreadSafePriorityQueue(5);
        var job = MakeJob(1);
        queue.TryEnqueue(job);
        Assert.True(queue.Contains(job.Id));
    }

    [Fact]
    public void Contains_NonExistingId_ReturnsFalse()
    {
        var queue = new ThreadSafePriorityQueue(5);
        Assert.False(queue.Contains(Guid.NewGuid()));
    }

    [Fact]
    public void ThreadSafety_ConcurrentEnqueue_CountStaysWithinLimit()
    {
        var queue = new ThreadSafePriorityQueue(50);
        var threads = Enumerable.Range(0, 10).Select(_ => new Thread(() =>
        {
            for (int i = 0; i < 20; i++)
                queue.TryEnqueue(MakeJob(i % 5 + 1));
        })).ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.True(queue.Count <= 50);
    }
}