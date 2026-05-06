namespace Kolokvijum_1.Tests;

public class ProgramTests
{
    // ── CreateRandomJob ───────────────────────────────────────────────────────

    [Fact]
    public void CreateRandomJob_ReturnsNonEmptyId()
    {
        var job = Program.CreateRandomJob();
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void CreateRandomJob_ReturnsValidJobType()
    {
        var job = Program.CreateRandomJob();
        Assert.True(job.Type == JobType.Prime || job.Type == JobType.IO);
    }

    [Fact]
    public void CreateRandomJob_PriorityInRange()
    {
        for (int i = 0; i < 20; i++)
        {
            var job = Program.CreateRandomJob();
            Assert.InRange(job.Priority, 1, 5);
        }
    }

    [Fact]
    public void CreateRandomJob_PrimePayload_HasCorrectFormat()
    {
        bool foundPrime = false;
        for (int i = 0; i < 50; i++)
        {
            var job = Program.CreateRandomJob();
            if (job.Type == JobType.Prime)
            {
                Assert.Contains("numbers:", job.Payload);
                Assert.Contains("threads:", job.Payload);
                foundPrime = true;
                break;
            }
        }
        Assert.True(foundPrime, "Should have generated at least one Prime job in 50 tries");
    }

    [Fact]
    public void CreateRandomJob_IOPayload_HasCorrectFormat()
    {
        bool foundIO = false;
        for (int i = 0; i < 50; i++)
        {
            var job = Program.CreateRandomJob();
            if (job.Type == JobType.IO)
            {
                Assert.Contains("delay:", job.Payload);
                foundIO = true;
                break;
            }
        }
        Assert.True(foundIO, "Should have generated at least one IO job in 50 tries");
    }

    [Fact]
    public void CreateRandomJob_TwoCallsProduceDifferentIds()
    {
        var job1 = Program.CreateRandomJob();
        var job2 = Program.CreateRandomJob();
        Assert.NotEqual(job1.Id, job2.Id);
    }

    [Fact]
    public void CreateRandomJob_PrimeThreadsInPayload_WithinValidRange()
    {
        for (int i = 0; i < 50; i++)
        {
            var job = Program.CreateRandomJob();
            if (job.Type == JobType.Prime)
            {
                var parts = JobProcessor.ParsePayload(job.Payload);
                var threads = int.Parse(parts["threads"]);
                Assert.InRange(threads, 1, 8);
                break;
            }
        }
    }

    // ── ProducerLoop ──────────────────────────────────────────────────────────

    [Fact]
    public void ProducerLoop_CancelledToken_ExitsImmediately()
    {
        var system = MakeSystem();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var thread = new Thread(() => Program.ProducerLoop(system, 0, cts.Token));
        thread.Start();
        bool finished = thread.Join(TimeSpan.FromSeconds(2));

        Assert.True(finished, "ProducerLoop should exit when token is already cancelled");
    }

    [Fact]
    public void ProducerLoop_RunsBrieflyThenCancels_DoesNotThrow()
    {
        var system = MakeSystem();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        var ex = Record.Exception(() =>
        {
            var thread = new Thread(() => Program.ProducerLoop(system, 0, cts.Token))
            {
                IsBackground = true
            };
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(3));
        });

        Assert.Null(ex);
    }

    [Fact]
    public void ProducerLoop_QueueFull_DoesNotThrow()
    {
        var fullSystem = new ProcessingSystem(
            workerCount: 0,
            maxQueueSize: 1,
            logFilePath: Path.GetTempFileName(),
            reportDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        );

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        var ex = Record.Exception(() =>
        {
            var thread = new Thread(() => Program.ProducerLoop(fullSystem, 0, cts.Token))
            {
                IsBackground = true
            };
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(2));
        });

        Assert.Null(ex);
    }

    private static ProcessingSystem MakeSystem() => new ProcessingSystem(
        workerCount: 2,
        maxQueueSize: 50,
        logFilePath: Path.GetTempFileName(),
        reportDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
    );
}
