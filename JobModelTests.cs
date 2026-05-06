namespace Kolokvijum_1.Tests;

public class JobModelTests
{
    [Fact]
    public void Job_DefaultValues_AreCorrect()
    {
        var job = new Job();
        Assert.Equal(Guid.Empty, job.Id);
        Assert.Equal(string.Empty, job.Payload);
        Assert.Equal(0, job.Priority);
    }

    [Fact]
    public void Job_CompareTo_LowerPriorityNumberComesFirst()
    {
        var high = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "", Priority = 1 };
        var low = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "", Priority = 5 };

        Assert.True(high.CompareTo(low) < 0);
        Assert.True(low.CompareTo(high) > 0);
    }

    [Fact]
    public void Job_CompareTo_SamePriority_ReturnsZero()
    {
        var a = new Job { Priority = 3 };
        var b = new Job { Priority = 3 };
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void Job_CompareTo_Null_ReturnsMinusOne()
    {
        var job = new Job { Priority = 1 };
        Assert.Equal(-1, job.CompareTo((Job?)null));
    }

    [Fact]
    public void JobHandle_CanSetProperties()
    {
        var id = Guid.NewGuid();
        var task = Task.FromResult(42);
        var handle = new JobHandle { Id = id, Result = task };

        Assert.Equal(id, handle.Id);
        Assert.Equal(task, handle.Result);
    }

    [Fact]
    public void JobCompletedEventArgs_PropertiesSetCorrectly()
    {
        var id = Guid.NewGuid();
        var now = DateTime.Now;
        var args = new JobCompletedEventArgs(id, 42, now);

        Assert.Equal(id, args.JobId);
        Assert.Equal(42, args.Result);
        Assert.Equal(now, args.CompletedAt);
    }

    [Fact]
    public void JobFailedEventArgs_PropertiesSetCorrectly()
    {
        var id = Guid.NewGuid();
        var now = DateTime.Now;
        var args = new JobFailedEventArgs(id, "timeout", aborted: true, now);

        Assert.Equal(id, args.JobId);
        Assert.Equal("timeout", args.Reason);
        Assert.True(args.Aborted);
        Assert.Equal(now, args.FailedAt);
    }

    [Fact]
    public void JobFailedEventArgs_NotAborted_AbortedIsFalse()
    {
        var args = new JobFailedEventArgs(Guid.NewGuid(), "fail", aborted: false, DateTime.Now);
        Assert.False(args.Aborted);
    }

    [Fact]
    public void JobResult_PropertiesSetCorrectly()
    {
        var id = Guid.NewGuid();
        var result = new JobResult
        {
            JobId = id,
            Type = JobType.Prime,
            Success = true,
            ReturnValue = 10,
            CompletedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(500)
        };

        Assert.Equal(id, result.JobId);
        Assert.Equal(JobType.Prime, result.Type);
        Assert.True(result.Success);
        Assert.Equal(10, result.ReturnValue);
        Assert.Equal(500, result.Duration.TotalMilliseconds);
    }

    [Fact]
    public void JobType_EnumValues_ExistForPrimeAndIO()
    {
        Assert.True(Enum.IsDefined(typeof(JobType), "Prime"));
        Assert.True(Enum.IsDefined(typeof(JobType), "IO"));
    }
}
