namespace Kolokvijum_1;

public class JobCompletedEventArgs : EventArgs
{
    public Guid JobId { get; }
    public int Result { get; }
    public DateTime CompletedAt { get; }

    public JobCompletedEventArgs(Guid jobId, int result, DateTime completedAt)
    {
        JobId = jobId;
        Result = result;
        CompletedAt = completedAt;
    }
}

public class JobFailedEventArgs : EventArgs
{
    public Guid JobId { get; }
    public string Reason { get; }
    public bool Aborted { get; }
    public DateTime FailedAt { get; }

    public JobFailedEventArgs(Guid jobId, string reason, bool aborted, DateTime failedAt)
    {
        JobId = jobId;
        Reason = reason;
        Aborted = aborted;
        FailedAt = failedAt;
    }
}
