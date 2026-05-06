namespace Kolokvijum_1;

public enum JobType
{
    Prime,
    IO
}

public class Job
{
    public Guid Id { get; set; }
    public JobType Type { get; set; }
    public string Payload { get; set; } = "";
    public int Priority { get; set; }

    public int CompareTo(Job? other)
    {
        if (other == null) return -1;
        return Priority.CompareTo(other.Priority);
    }
}
