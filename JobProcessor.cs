namespace Kolokvijum_1;

public static class JobProcessor
{
    private static readonly Random _random = new();

    /// <summary>
    /// Processes a job based on its type. Payload is parsed here.
    /// </summary>
    public static async Task<int> ProcessAsync(Job job)
    {
        return job.Type switch
        {
            JobType.Prime => await ProcessPrimeAsync(job.Payload),
            JobType.IO => await ProcessIOAsync(job.Payload),
            _ => throw new ArgumentException($"Unknown job type: {job.Type}")
        };
    }

    private static Task<int> ProcessPrimeAsync(string payload)
    {
        // Payload format: "numbers:10_000,threads:3"
        var parts = ParsePayload(payload);

        if (!parts.TryGetValue("numbers", out var numbersStr) || !int.TryParse(numbersStr, out var limit))
            throw new ArgumentException($"Invalid Prime payload: {payload}");

        if (!parts.TryGetValue("threads", out var threadsStr) || !int.TryParse(threadsStr, out var threadCount))
            throw new ArgumentException($"Invalid Prime payload (threads): {payload}");

        // Clamp threads to [1, 8]
        threadCount = Math.Clamp(threadCount, 1, 8);

        return Task.Run(() =>
        {
            // Parallel prime counting using Parallel.For with degree of parallelism
            var count = 0;
            var options = new ParallelOptions { MaxDegreeOfParallelism = threadCount };

            Parallel.For(2, limit + 1, options, () => 0, (i, _, localCount) =>
            {
                if (IsPrime(i)) localCount++;
                return localCount;
            }, localCount => Interlocked.Add(ref count, localCount));

            return count;
        });
    }

    private static Task<int> ProcessIOAsync(string payload)
    {
        // Payload format: "delay:1_000"
        var parts = ParsePayload(payload);

        if (!parts.TryGetValue("delay", out var delayStr) || !int.TryParse(delayStr, out var delayMs))
            throw new ArgumentException($"Invalid IO payload: {payload}");

        return Task.Run(() =>
        {
            Thread.Sleep(delayMs);
            lock (_random)
            {
                return _random.Next(0, 101);
            }
        });
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;
        for (int i = 3; i * i <= n; i += 2)
            if (n % i == 0) return false;
        return true;
    }

    /// <summary>
    /// Parses "key:value,key2:value2" format, stripping underscores from numeric values.
    /// </summary>
    public static Dictionary<string, string> ParsePayload(string payload)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in payload.Split(','))
        {
            var kv = part.Split(':', 2);
            if (kv.Length == 2)
                result[kv[0].Trim()] = kv[1].Trim().Replace("_", "");
        }
        return result;
    }
}
