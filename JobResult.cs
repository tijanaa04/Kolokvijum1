namespace Kolokvijum_1;

public class JobResult
{
    public Guid JobId { get; set; }
    public JobType Type { get; set; }
    public bool Success { get; set; }
    public int ReturnValue { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
