using Kolokvijum_1.Config;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Kolokvijum_1.Tests")]

namespace Kolokvijum_1;

public class Program
{
    internal static readonly Random _random = new();

    static async Task Main(string[] args)
    {
        // ── Load configuration 
        var configPath = args.Length > 0 ? args[0] : "SystemConfig.xml";

        SystemConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
            Console.WriteLine($"[Config] Loaded: {config.WorkerCount} workers, max queue {config.MaxQueueSize}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fatal] Failed to load config: {ex.Message}");
            return;
        }

        // ── Create ProcessingSystem 
        var system = new ProcessingSystem(config.WorkerCount, config.MaxQueueSize);

        // ── Load initial jobs from XML 
        try
        {
            var initialJobs = ConfigLoader.LoadInitialJobs(config);
            Console.WriteLine($"[Init] Submitting {initialJobs.Count} initial jobs from config...");
            foreach (var job in initialJobs)
            {
                var handle = system.Submit(job);
                if (handle == null)
                    Console.WriteLine($"[Init] Job {job.Id} rejected (queue full or duplicate).");
                else
                    Console.WriteLine($"[Init] Job {job.Id} [{job.Type}] submitted with priority {job.Priority}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Init] Error loading initial jobs: {ex.Message}");
        }

        // ── Start producer threads (count from config = WorkerCount) 
        int producerCount = config.WorkerCount;
        var producerThreads = new List<Thread>();
        var cts = new CancellationTokenSource();

        for (int i = 0; i < producerCount; i++)
        {
            int threadId = i;
            var thread = new Thread(() => ProducerLoop(system, threadId, cts.Token))
            {
                IsBackground = true,
                Name = $"Producer-{threadId}"
            };
            producerThreads.Add(thread);
            thread.Start();
        }

        Console.WriteLine($"[Main] {producerCount} producer threads running. Press ENTER to stop.");
        Console.ReadLine();

        cts.Cancel();

        // Give producers a moment to stop
        await Task.Delay(500);

        Console.WriteLine("[Main] Shutting down.");
    }

    internal static void ProducerLoop(ProcessingSystem system, int threadId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var job = CreateRandomJob();
                var handle = system.Submit(job);

                if (handle == null)
                    Console.WriteLine($"[Producer-{threadId}] Queue full, job rejected.");
                else
                    Console.WriteLine($"[Producer-{threadId}] Submitted job {job.Id} [{job.Type}] priority={job.Priority}");

                // Random delay between 200ms and 1.5s
                int delay;
                lock (_random) { delay = _random.Next(200, 1500); }
                Thread.Sleep(delay);
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Producer-{threadId}] Error: {ex.Message}");
            }
        }
    }

    internal static Job CreateRandomJob()
    {
        JobType type;
        string payload;
        int priority;

        lock (_random)
        {
            type = _random.Next(2) == 0 ? JobType.Prime : JobType.IO;
            priority = _random.Next(1, 6); // 1 (highest) to 5 (lowest)

            if (type == JobType.Prime)
            {
                var limit = _random.Next(1000, 50000);
                var threads = _random.Next(1, 9); // clamped to [1,8] in processor
                payload = $"numbers:{limit},threads:{threads}";
            }
            else
            {
                var delay = _random.Next(100, 3000);
                payload = $"delay:{delay}";
            }
        }

        return new Job
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            Priority = priority
        };
    }
}
