using System.Xml.Serialization;

namespace Kolokvijum_1.Config;

public static class ConfigLoader
{
    public static SystemConfig Load(string path)
    {
        var serializer = new XmlSerializer(typeof(SystemConfig));
        using var reader = new StreamReader(path);
        var config = (SystemConfig?)serializer.Deserialize(reader);
        return config ?? throw new InvalidOperationException("Failed to deserialize SystemConfig.xml");
    }

    public static List<Job> LoadInitialJobs(SystemConfig config)
    {
        var jobs = new List<Job>();
        foreach (var entry in config.Jobs)
        {
            if (!Enum.TryParse<JobType>(entry.Type, out var jobType))
                throw new InvalidOperationException($"Unknown JobType: {entry.Type}");

            jobs.Add(new Job
            {
                Id = Guid.NewGuid(),
                Type = jobType,
                Payload = entry.Payload,
                Priority = entry.Priority
            });
        }
        return jobs;
    }
}