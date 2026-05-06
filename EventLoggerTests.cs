namespace Kolokvijum_1.Tests;

public class EventLoggerTests
{
    private readonly string _logFile;
    private readonly EventLogger _logger;

    public EventLoggerTests()
    {
        _logFile = Path.GetTempFileName();
        _logger = new EventLogger(_logFile);
    }

    public void Dispose()
    {
        if (File.Exists(_logFile)) File.Delete(_logFile);
    }

    [Fact]
    public async Task LogAsync_WritesLineToFile()
    {
        var id = Guid.NewGuid();
        await _logger.LogAsync("COMPLETED", id, "42");

        var lines = await File.ReadAllLinesAsync(_logFile);
        Assert.Single(lines);
        Assert.Contains("COMPLETED", lines[0]);
        Assert.Contains(id.ToString(), lines[0]);
        Assert.Contains("42", lines[0]);
    }

    [Fact]
    public async Task LogAsync_MultipleWrites_AllLinesPresent()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await _logger.LogAsync("COMPLETED", id1, "10");
        await _logger.LogAsync("FAILED", id2, "error");

        var lines = await File.ReadAllLinesAsync(_logFile);
        Assert.Equal(2, lines.Length);
        Assert.Contains("COMPLETED", lines[0]);
        Assert.Contains("FAILED", lines[1]);
    }

    [Fact]
    public async Task LogAsync_ContainsDateTimeFormat()
    {
        await _logger.LogAsync("ABORT", Guid.NewGuid(), "0");
        var content = await File.ReadAllTextAsync(_logFile);
        // Should contain date in [yyyy-MM-dd HH:mm:ss] format
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]", content);
    }

    [Fact]
    public async Task LogAsync_ConcurrentWrites_AllLinesWritten()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => _logger.LogAsync("COMPLETED", Guid.NewGuid(), i.ToString()));

        await Task.WhenAll(tasks);

        var lines = await File.ReadAllLinesAsync(_logFile);
        Assert.Equal(20, lines.Length);
    }

    [Fact]
    public async Task LogAsync_AbortStatus_WrittenCorrectly()
    {
        var id = Guid.NewGuid();
        await _logger.LogAsync("ABORT", id, "timeout");

        var content = await File.ReadAllTextAsync(_logFile);
        Assert.Contains("ABORT", content);
        Assert.Contains(id.ToString(), content);
    }
}
