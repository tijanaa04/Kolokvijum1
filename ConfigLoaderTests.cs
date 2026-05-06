using Kolokvijum_1.Config;

namespace Kolokvijum_1.Tests;

public class ConfigLoaderTests
{
    private readonly string _tempFile;

    public ConfigLoaderTests()
    {
        _tempFile = Path.GetTempFileName() + ".xml";
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private void WriteConfig(string xml) => File.WriteAllText(_tempFile, xml);

    [Fact]
    public void Load_ValidXml_ReturnsConfig()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SystemConfig>
  <WorkerCount>3</WorkerCount>
  <MaxQueueSize>50</MaxQueueSize>
  <Jobs>
    <Job Type=""IO"" Payload=""delay:100"" Priority=""1""/>
  </Jobs>
</SystemConfig>");

        var config = ConfigLoader.Load(_tempFile);
        Assert.Equal(3, config.WorkerCount);
        Assert.Equal(50, config.MaxQueueSize);
        Assert.Single(config.Jobs);
    }

    [Fact]
    public void Load_MultipleJobs_AllLoaded()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SystemConfig>
  <WorkerCount>5</WorkerCount>
  <MaxQueueSize>100</MaxQueueSize>
  <Jobs>
    <Job Type=""Prime"" Payload=""numbers:10_000,threads:3"" Priority=""1""/>
    <Job Type=""IO"" Payload=""delay:1_000"" Priority=""2""/>
    <Job Type=""IO"" Payload=""delay:3_000"" Priority=""3""/>
  </Jobs>
</SystemConfig>");

        var config = ConfigLoader.Load(_tempFile);
        Assert.Equal(3, config.Jobs.Count);
        Assert.Equal("Prime", config.Jobs[0].Type);
        Assert.Equal("IO", config.Jobs[1].Type);
    }

    [Fact]
    public void Load_InvalidPath_Throws()
    {
        Assert.ThrowsAny<Exception>(() => ConfigLoader.Load("nonexistent_file.xml"));
    }

    [Fact]
    public void LoadInitialJobs_ValidConfig_ReturnsJobs()
    {
        var config = new SystemConfig
        {
            WorkerCount = 2,
            MaxQueueSize = 10,
            Jobs = new List<JobConfigEntry>
            {
                new JobConfigEntry { Type = "Prime", Payload = "numbers:100,threads:2", Priority = 1 },
                new JobConfigEntry { Type = "IO", Payload = "delay:500", Priority = 2 }
            }
        };

        var jobs = ConfigLoader.LoadInitialJobs(config);
        Assert.Equal(2, jobs.Count);
        Assert.Equal(JobType.Prime, jobs[0].Type);
        Assert.Equal(JobType.IO, jobs[1].Type);
        Assert.Equal(1, jobs[0].Priority);
    }

    [Fact]
    public void LoadInitialJobs_AssignsNewGuids()
    {
        var config = new SystemConfig
        {
            Jobs = new List<JobConfigEntry>
            {
                new JobConfigEntry { Type = "IO", Payload = "delay:100", Priority = 1 },
                new JobConfigEntry { Type = "IO", Payload = "delay:200", Priority = 2 }
            }
        };

        var jobs = ConfigLoader.LoadInitialJobs(config);
        Assert.NotEqual(jobs[0].Id, jobs[1].Id);
        Assert.NotEqual(Guid.Empty, jobs[0].Id);
    }

    [Fact]
    public void LoadInitialJobs_InvalidJobType_Throws()
    {
        var config = new SystemConfig
        {
            Jobs = new List<JobConfigEntry>
            {
                new JobConfigEntry { Type = "UnknownType", Payload = "x:1", Priority = 1 }
            }
        };

        Assert.Throws<InvalidOperationException>(() => ConfigLoader.LoadInitialJobs(config));
    }

    [Fact]
    public void LoadInitialJobs_EmptyJobs_ReturnsEmptyList()
    {
        var config = new SystemConfig { Jobs = new List<JobConfigEntry>() };
        var jobs = ConfigLoader.LoadInitialJobs(config);
        Assert.Empty(jobs);
    }
}
