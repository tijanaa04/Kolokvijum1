namespace Kolokvijum_1;

public class EventLogger
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public EventLogger(string logFilePath = "job_events.log")
    {
        _logFilePath = logFilePath;
    }

    public async Task LogAsync(string status, Guid jobId, string result)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{status}] {jobId}, {result}";
        await _semaphore.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
