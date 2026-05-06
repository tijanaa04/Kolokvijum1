using System.Xml.Linq;

namespace Kolokvijum_1.Tests;

public class ReportGeneratorTests
{
    private readonly string _reportDir;
    private readonly ReportGenerator _generator;

    public ReportGeneratorTests()
    {
        _reportDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _generator = new ReportGenerator(_reportDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_reportDir))
            Directory.Delete(_reportDir, true);
    }

    private static JobResult MakeResult(JobType type, bool success, double durationMs) => new JobResult
    {
        JobId = Guid.NewGuid(),
        Type = type,
        Success = success,
        ReturnValue = 42,
        CompletedAt = DateTime.UtcNow,
        Duration = TimeSpan.FromMilliseconds(durationMs)
    };

    [Fact]
    public async Task GenerateReportAsync_CreatesReportFile()
    {
        await _generator.GenerateReportAsync(
            new[] { MakeResult(JobType.IO, true, 100) },
            Array.Empty<JobResult>()
        );

        var files = Directory.GetFiles(_reportDir, "report_*.xml");
        Assert.Single(files);
    }

    [Fact]
    public async Task GenerateReportAsync_FileContainsValidXml()
    {
        await _generator.GenerateReportAsync(
            new[] { MakeResult(JobType.Prime, true, 200) },
            Array.Empty<JobResult>()
        );

        var file = Directory.GetFiles(_reportDir, "report_*.xml").First();
        var doc = XDocument.Load(file);
        Assert.NotNull(doc.Root);
        Assert.Equal("Report", doc.Root!.Name.LocalName);
    }

    [Fact]
    public async Task GenerateReportAsync_CompletedJobsGroupedByType()
    {
        var completed = new[]
        {
            MakeResult(JobType.IO, true, 100),
            MakeResult(JobType.IO, true, 200),
            MakeResult(JobType.Prime, true, 500)
        };

        await _generator.GenerateReportAsync(completed, Array.Empty<JobResult>());

        var file = Directory.GetFiles(_reportDir, "report_*.xml").First();
        var doc = XDocument.Load(file);
        var stats = doc.Root!.Element("CompletedJobs")!.Elements("TypeStats").ToList();

        Assert.Equal(2, stats.Count);
        var ioStat = stats.First(s => s.Attribute("Type")!.Value == "IO");
        Assert.Equal("2", ioStat.Attribute("Count")!.Value);
    }

    [Fact]
    public async Task GenerateReportAsync_FailedJobsGroupedByType()
    {
        var failed = new[]
        {
            MakeResult(JobType.IO, false, 2000),
            MakeResult(JobType.IO, false, 2000),
            MakeResult(JobType.Prime, false, 2000)
        };

        await _generator.GenerateReportAsync(Array.Empty<JobResult>(), failed);

        var file = Directory.GetFiles(_reportDir, "report_*.xml").First();
        var doc = XDocument.Load(file);
        var stats = doc.Root!.Element("FailedJobs")!.Elements("TypeStats").ToList();

        Assert.Equal(2, stats.Count);
        var ioFailed = stats.First(s => s.Attribute("Type")!.Value == "IO");
        Assert.Equal("2", ioFailed.Attribute("Count")!.Value);
    }

    [Fact]
    public async Task GenerateReportAsync_ReportRotatesAfter10()
    {
        for (int i = 0; i < 12; i++)
        {
            await _generator.GenerateReportAsync(
                new[] { MakeResult(JobType.IO, true, 100) },
                Array.Empty<JobResult>()
            );
        }

        var files = Directory.GetFiles(_reportDir, "report_*.xml");
        // Only 10 slots: report_0.xml .. report_9.xml
        Assert.Equal(10, files.Length);
    }

    [Fact]
    public async Task GenerateReportAsync_EmptyInputs_CreatesEmptyReport()
    {
        await _generator.GenerateReportAsync(
            Array.Empty<JobResult>(),
            Array.Empty<JobResult>()
        );

        var file = Directory.GetFiles(_reportDir, "report_*.xml").First();
        var doc = XDocument.Load(file);
        Assert.Empty(doc.Root!.Element("CompletedJobs")!.Elements());
        Assert.Empty(doc.Root!.Element("FailedJobs")!.Elements());
    }

    [Fact]
    public async Task GenerateReportAsync_AvgDurationIsCorrect()
    {
        var completed = new[]
        {
            MakeResult(JobType.IO, true, 100),
            MakeResult(JobType.IO, true, 300)
        };

        await _generator.GenerateReportAsync(completed, Array.Empty<JobResult>());

        var file = Directory.GetFiles(_reportDir, "report_*.xml").First();
        var doc = XDocument.Load(file);
        var ioStat = doc.Root!.Element("CompletedJobs")!.Elements("TypeStats")
            .First(s => s.Attribute("Type")!.Value == "IO");

        var avg = double.Parse(ioStat.Attribute("AvgDurationMs")!.Value);
        Assert.Equal(200.0, avg, precision: 1);
    }
}
