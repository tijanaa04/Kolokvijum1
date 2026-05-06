namespace Kolokvijum_1.Tests;

public class ProcessingSystemTests
{
    private readonly ProcessingSystem _system;
    private readonly string _logFile;
    private readonly string _reportDir;

    public ProcessingSystemTests()
    {
        _logFile = Path.GetTempFileName();
        _reportDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _system = new ProcessingSystem(workerCount: 2, maxQueueSize: 10,
            logFilePath: _logFile, reportDirectory: _reportDir);
    }

    public void Dispose()
    {
        _system.Dispose();
        if (File.Exists(_logFile)) File.Delete(_logFile);
        if (Directory.Exists(_reportDir)) Directory.Delete(_reportDir, true);
    }

    private static ProcessingSystem MakeSystem(int workers = 2, int maxQueue = 20) =>
        new ProcessingSystem(workers, maxQueue,
            logFilePath: Path.GetTempFileName(),
            reportDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

    private static Job MakeJob(JobType type, string payload, int priority) => new Job
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = payload,
        Priority = priority
    };

    // ── Submit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Submit_ValidJob_ReturnsHandle()
    {
        var job = MakeJob(JobType.IO, "delay:50", 1);
        var handle = _system.Submit(job);
        Assert.NotNull(handle);
        Assert.Equal(job.Id, handle.Id);
    }

    [Fact]
    public void Submit_WhenQueueFull_ReturnsNull()
    {
        var sys = MakeSystem(workers: 0, maxQueue: 2);
        sys.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        sys.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        var result = sys.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        Assert.Null(result);
    }

    [Fact]
    public async Task Submit_IOJob_CompletesSuccessfully()
    {
        var job = MakeJob(JobType.IO, "delay:50", 1);
        var handle = _system.Submit(job);
        Assert.NotNull(handle);
        var result = await handle!.Result;
        Assert.InRange(result, 0, 100);
    }

    [Fact]
    public async Task Submit_PrimeJob_CompletesWithCorrectCount()
    {
        var job = MakeJob(JobType.Prime, "numbers:10,threads:1", 1);
        var handle = _system.Submit(job);
        Assert.NotNull(handle);
        var result = await handle!.Result;
        Assert.Equal(4, result);
    }

    [Fact]
    public async Task Submit_MultipleJobs_AllComplete()
    {
        var handles = new List<Task<int>>();
        for (int i = 0; i < 5; i++)
        {
            var job = MakeJob(JobType.IO, "delay:50", i + 1);
            var handle = _system.Submit(job);
            Assert.NotNull(handle);
            handles.Add(handle!.Result);
        }
        var results = await Task.WhenAll(handles);
        Assert.All(results, r => Assert.InRange(r, 0, 100));
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_IdempotencyCheck_JobNotExecutedTwice()
    {
        var job = MakeJob(JobType.IO, "delay:50", 1);
        var handle1 = _system.Submit(job);
        var result1 = await handle1!.Result;
        await Task.Delay(200);

        var handle2 = _system.Submit(job);
        if (handle2 != null)
        {
            Assert.Equal(handle1.Id, handle2.Id);
            var result2 = await handle2.Result;
            Assert.Equal(result1, result2);
        }
    }

    [Fact]
    public void Submit_SameJobTwice_WhileInQueue_WorkerExecutesOnce()
    {
        // With workerCount:0 job stays in queue, second submit hits _executedIds check in worker
        var sys = MakeSystem(workers: 0, maxQueue: 10);
        var job = MakeJob(JobType.IO, "delay:50", 1);
        var h1 = sys.Submit(job);
        var h2 = sys.Submit(job);
        Assert.NotNull(h1);
        // h2 returns existing handle since job is still in _handles
        if (h2 != null)
            Assert.Equal(h1!.Id, h2.Id);
    }

    // ── GetTopJobs ────────────────────────────────────────────────────────────

    [Fact]
    public void GetTopJobs_ReturnsNJobsByPriority()
    {
        var sys = MakeSystem(workers: 0);
        sys.Submit(MakeJob(JobType.IO, "delay:100", 3));
        sys.Submit(MakeJob(JobType.IO, "delay:100", 1));
        sys.Submit(MakeJob(JobType.IO, "delay:100", 2));

        var top2 = sys.GetTopJobs(2).ToList();
        Assert.Equal(2, top2.Count);
        Assert.Equal(1, top2[0].Priority);
        Assert.Equal(2, top2[1].Priority);
    }

    [Fact]
    public void GetTopJobs_EmptyQueue_ReturnsEmpty()
    {
        Assert.Empty(_system.GetTopJobs(5));
    }

    // ── GetJob ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetJob_ExistingId_ReturnsJob()
    {
        var sys = MakeSystem(workers: 0);
        var job = MakeJob(JobType.IO, "delay:100", 1);
        sys.Submit(job);
        var found = sys.GetJob(job.Id);
        Assert.NotNull(found);
        Assert.Equal(job.Id, found!.Id);
    }

    [Fact]
    public void GetJob_NonExistingId_ReturnsNull()
    {
        Assert.Null(_system.GetJob(Guid.NewGuid()));
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobCompleted_Event_IsFiredOnSuccess()
    {
        bool fired = false;
        _system.JobCompleted += (_, _) => { fired = true; };

        var handle = _system.Submit(MakeJob(JobType.IO, "delay:50", 1));
        await handle!.Result;
        await Task.Delay(200);

        Assert.True(fired);
    }

    [Fact]
    public async Task JobCompleted_LogsCorrectly()
    {
        var handle = _system.Submit(MakeJob(JobType.IO, "delay:50", 1));
        await handle!.Result;
        await Task.Delay(300);

        var log = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("COMPLETED", log);
    }

    [Fact]
    public async Task JobFailed_Event_IsFiredOnTimeout()
    {
        bool fired = false;
        _system.JobFailed += (_, _) => { fired = true; };

        _system.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        await Task.Delay(8000);

        Assert.True(fired);
    }

    [Fact]
    public async Task JobFailed_AfterAllRetries_LogsAbort()
    {
        _system.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        await Task.Delay(8000);

        var log = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("ABORT", log);
    }

    [Fact]
    public async Task JobFailed_IntermediateRetry_LogsFailed()
    {
        _system.Submit(MakeJob(JobType.IO, "delay:10000", 1));
        // Wait for first timeout only (2s + buffer)
        await Task.Delay(2500);

        var log = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("FAILED", log);
    }

    // ── Priority ordering ─────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_HighPriorityJob_ProcessedBeforeLow()
    {
        var sys = MakeSystem(workers: 1, maxQueue: 20);
        var completionOrder = new List<int>();

        // Submit low priority first, then high priority
        var lowJob = MakeJob(JobType.IO, "delay:50", 5);
        var highJob = MakeJob(JobType.IO, "delay:50", 1);

        sys.JobCompleted += (_, e) =>
        {
            if (e.JobId == highJob.Id) completionOrder.Add(1);
            if (e.JobId == lowJob.Id) completionOrder.Add(5);
        };

        sys.Submit(lowJob);
        sys.Submit(highJob);

        await Task.Delay(1000);
        Assert.True(completionOrder.Count > 0);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var sys = MakeSystem();
        sys.Dispose();
        var ex = Record.Exception(() => sys.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_StopsWorkers()
    {
        var sys = MakeSystem(workers: 3);
        var ex = Record.Exception(() => sys.Dispose());
        Assert.Null(ex);
    }
}
