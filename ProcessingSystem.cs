namespace Kolokvijum_1;
using System.Collections.Concurrent;
using System.Xml.Linq;

public class ProcessingSystem
{
    // ─── Events 
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;
    public event EventHandler<JobFailedEventArgs>? JobFailed;

    // ─── State 
    private readonly ThreadSafePriorityQueue _queue;
    private readonly ConcurrentDictionary<Guid, JobHandle> _handles = new();
    private readonly ConcurrentDictionary<Guid, bool> _executedIds = new();   // idempotency
    private readonly ConcurrentBag<JobResult> _completedResults = new();
    private readonly ConcurrentBag<JobResult> _failedResults = new();

    private readonly EventLogger _logger;
    private readonly ReportGenerator _reportGenerator;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Thread> _workerThreads = new();
    private readonly SemaphoreSlim _workAvailable = new(0);
    private readonly int _workerCount;
    private bool _disposed;

    private const int TimeoutMs = 2000;
    private const int MaxRetries = 2; // retry 2 times (3 total attempts)


    public ProcessingSystem(int workerCount, int maxQueueSize, string logFilePath = "job_events.log", string reportDirectory = "reports")
    {
        _workerCount = workerCount;
        _queue = new ThreadSafePriorityQueue(maxQueueSize);
        _logger = new EventLogger(logFilePath);
        _reportGenerator = new ReportGenerator(reportDirectory);

        // Subscribe to events with lambda expressions (as required)
        JobCompleted += async (_, e) =>
            await _logger.LogAsync("COMPLETED", e.JobId, e.Result.ToString());

        JobFailed += async (_, e) =>
        {
            var status = e.Aborted ? "ABORT" : "FAILED";
            await _logger.LogAsync(status, e.JobId, e.Reason);
        };

        StartWorkers();
        StartReportTimer();
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a job to the processing queue. Returns a JobHandle for awaiting the result.
    /// Returns null if the queue is full or job already submitted (idempotency).
    /// </summary>
    public JobHandle? Submit(Job job)
    {
        // Idempotency: same Id must not be executed more than once
        if (_executedIds.ContainsKey(job.Id))
            return _handles.TryGetValue(job.Id, out var existing) ? existing : null;

        if (!_queue.TryEnqueue(job))
            return null; // queue full

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new JobHandle { Id = job.Id, Result = tcs.Task };
        _handles[job.Id] = handle;

        // Store TCS so workers can resolve it
        _pendingTcs[job.Id] = tcs;

        _workAvailable.Release();
        return handle;
    }

    /// <summary>Returns top N jobs by priority from the active queue.</summary>
    public IEnumerable<Job> GetTopJobs(int n) => _queue.GetTopN(n);

    /// <summary>Returns the job object for a given ID.</summary>
    public Job? GetJob(Guid id)
    {
        // Search queue
        return _queue.GetTopN(int.MaxValue).FirstOrDefault(j => j.Id == id);
    }

    // ─── Internal TCS storage ──────────────────────────────────────────────────
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<int>> _pendingTcs = new();

    // ─── Worker threads ────────────────────────────────────────────────────────
    private void StartWorkers()
    {
        for (int i = 0; i < _workerCount; i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"Worker-{i}"
            };
            _workerThreads.Add(thread);
            thread.Start();
        }
    }

    private void WorkerLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _workAvailable.Wait(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_queue.TryDequeue(out var job) || job == null)
                continue;

            // Idempotency guard: mark as executing
            if (!_executedIds.TryAdd(job.Id, true))
            {
                // Already handled by another worker
                _pendingTcs.TryRemove(job.Id, out _);
                continue;
            }

            _ = ExecuteWithRetryAsync(job);
        }
    }

    private async Task ExecuteWithRetryAsync(Job job)
    {
        _pendingTcs.TryGetValue(job.Id, out var tcs);

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                using var cts = new CancellationTokenSource(TimeoutMs);
                var processTask = JobProcessor.ProcessAsync(job);

                // Wait with timeout
                var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeoutMs));

                if (completedTask != processTask)
                    throw new TimeoutException($"Job {job.Id} exceeded {TimeoutMs}ms timeout.");

                var result = await processTask;
                var duration = DateTime.UtcNow - startTime;

                // Success
                _completedResults.Add(new JobResult
                {
                    JobId = job.Id,
                    Type = job.Type,
                    Success = true,
                    ReturnValue = result,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration
                });

                tcs?.SetResult(result);
                JobCompleted?.Invoke(this, new JobCompletedEventArgs(job.Id, result, DateTime.UtcNow));
                _pendingTcs.TryRemove(job.Id, out _);
                return;
            }
            catch (Exception ex)
            {
                bool isLastAttempt = attempt == MaxRetries;

                if (isLastAttempt)
                {
                    // ABORT after 3rd failure
                    _failedResults.Add(new JobResult
                    {
                        JobId = job.Id,
                        Type = job.Type,
                        Success = false,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - (DateTime.UtcNow - TimeSpan.FromMilliseconds(TimeoutMs))
                    });

                    tcs?.SetException(ex);
                    JobFailed?.Invoke(this, new JobFailedEventArgs(job.Id, ex.Message, aborted: true, DateTime.UtcNow));
                    _pendingTcs.TryRemove(job.Id, out _);
                }
                else
                {
                    // Intermediate failure — fire JobFailed but retry
                    JobFailed?.Invoke(this, new JobFailedEventArgs(job.Id, ex.Message, aborted: false, DateTime.UtcNow));
                    await Task.Delay(100); // brief pause before retry
                }
            }
        }
    }

    // ─── Report timer ──────────────────────────────────────────────────────────
    private void StartReportTimer()
    {
        var timer = new System.Timers.Timer(60_000); // every minute
        timer.Elapsed += async (_, _) =>
        {
            try
            {
                await _reportGenerator.GenerateReportAsync(
                    _completedResults.ToArray(),
                    _failedResults.ToArray()
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReportTimer] Error generating report: {ex.Message}");
            }
        };
        timer.AutoReset = true;
        timer.Start();
    }

    // ─── Dispose ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _workAvailable.Dispose();
        _cts.Dispose();
    }
}

