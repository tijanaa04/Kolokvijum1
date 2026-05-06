namespace Kolokvijum_1.Tests;

public class JobProcessorTests
{
    // ─── ParsePayload 

    [Fact]
    public void ParsePayload_ValidPrime_ReturnsCorrectValues()
    {
        var result = JobProcessor.ParsePayload("numbers:10_000,threads:3");
        Assert.Equal("10000", result["numbers"]);
        Assert.Equal("3", result["threads"]);
    }

    [Fact]
    public void ParsePayload_ValidIO_ReturnsCorrectDelay()
    {
        var result = JobProcessor.ParsePayload("delay:1_500");
        Assert.Equal("1500", result["delay"]);
    }

    [Fact]
    public void ParsePayload_EmptyPayload_ReturnsEmptyDict()
    {
        var result = JobProcessor.ParsePayload("");
        Assert.Empty(result);
    }

    // ─── Prime job ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PrimeJob_ReturnsCorrectCount()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Prime,
            Payload = "numbers:10,threads:1",
            Priority = 1
        };

        var result = await JobProcessor.ProcessAsync(job);
        // Primes up to 10: 2,3,5,7 = 4
        Assert.Equal(4, result);
    }

    [Fact]
    public async Task ProcessAsync_PrimeJob_ClampsThreadsToMax8()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Prime,
            Payload = "numbers:20,threads:99",
            Priority = 1
        };
        // Should not throw; threads clamped to 8
        var result = await JobProcessor.ProcessAsync(job);
        // Primes up to 20: 2,3,5,7,11,13,17,19 = 8
        Assert.Equal(8, result);
    }

    [Fact]
    public async Task ProcessAsync_PrimeJob_ClampsThreadsToMin1()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Prime,
            Payload = "numbers:10,threads:0",
            Priority = 1
        };
        var result = await JobProcessor.ProcessAsync(job);
        Assert.Equal(4, result);
    }

    [Fact]
    public async Task ProcessAsync_PrimeJob_InvalidPayload_Throws()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Prime,
            Payload = "bad_payload",
            Priority = 1
        };
        await Assert.ThrowsAsync<ArgumentException>(() => JobProcessor.ProcessAsync(job));
    }

    // ─── IO job 

    [Fact]
    public async Task ProcessAsync_IOJob_ReturnsValueBetween0And100()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.IO,
            Payload = "delay:50",
            Priority = 2
        };

        var result = await JobProcessor.ProcessAsync(job);
        Assert.InRange(result, 0, 100);
    }

    [Fact]
    public async Task ProcessAsync_IOJob_InvalidPayload_Throws()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.IO,
            Payload = "wrong:stuff",
            Priority = 2
        };
        await Assert.ThrowsAsync<ArgumentException>(() => JobProcessor.ProcessAsync(job));
    }

    [Fact]
    public async Task ProcessAsync_UnknownJobType_Throws()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = (JobType)99,
            Payload = "x:1",
            Priority = 1
        };
        await Assert.ThrowsAsync<ArgumentException>(() => JobProcessor.ProcessAsync(job));
    }
}
