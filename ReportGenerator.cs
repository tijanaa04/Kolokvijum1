using System.Xml.Linq;

namespace Kolokvijum_1;

public class ReportGenerator
{
    private readonly string _reportDirectory;
    private const int MaxReports = 10;
    private int _reportIndex = 0;
    private readonly object _lock = new();

    public ReportGenerator(string reportDirectory = "reports")
    {
        _reportDirectory = reportDirectory;
        Directory.CreateDirectory(_reportDirectory);
    }

    public async Task GenerateReportAsync(IEnumerable<JobResult> completedJobs, IEnumerable<JobResult> failedJobs)
    {
        var allCompleted = completedJobs.ToList();
        var allFailed = failedJobs.ToList();

        // LINQ queries
        var completedByType = allCompleted
            .GroupBy(j => j.Type)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                AvgDurationMs = g.Average(j => j.Duration.TotalMilliseconds)
            })
            .OrderBy(x => x.Type.ToString())
            .ToList();

        var failedByType = allFailed
            .GroupBy(j => j.Type)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Type.ToString())
            .ToList();

        var report = new XDocument(
            new XElement("Report",
                new XAttribute("GeneratedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("CompletedJobs",
                    completedByType.Select(g =>
                        new XElement("TypeStats",
                            new XAttribute("Type", g.Type),
                            new XAttribute("Count", g.Count),
                            new XAttribute("AvgDurationMs", Math.Round(g.AvgDurationMs, 2))
                        )
                    )
                ),
                new XElement("FailedJobs",
                    failedByType.Select(g =>
                        new XElement("TypeStats",
                            new XAttribute("Type", g.Type),
                            new XAttribute("Count", g.Count)
                        )
                    )
                )
            )
        );

        int index;
        lock (_lock)
        {
            index = _reportIndex % MaxReports;
            _reportIndex++;
        }

        var filePath = Path.Combine(_reportDirectory, $"report_{index}.xml");
        await File.WriteAllTextAsync(filePath, report.ToString());
    }
}
