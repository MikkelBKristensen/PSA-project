using System.Diagnostics;
using System.Text.Json;
using Fuzzers.Harness;

namespace Fuzzers.Runner;

/// <summary>
/// One fuzzing process. Runs inputs from a strategy against all parsers until its parent goes away, and
/// reports new findings and statistics to the supervisor as JSON lines on stdout.
/// </summary>
public static class Worker
{
    public static int Run(Options o)
    {
        int seed = o.Int("seed", 0);
        int parentPid = o.Int("parent", -1);
        var source = InputSources.Create(o.Get("strategy", "grammar-mutation"), o.Int("max-depth", 6));
        var parsers = JsonParsers.All();
        using var shared = SharedInput.Open(o.Get("shared", ""));

        var rand = new Random(seed);
        var shortest = new Dictionary<string, int>();   // signature -> shortest input length reported
        var hits = new Dictionary<string, long>();      // signature -> hits since last stats message
        var outcomes = new Outcome[parsers.Length];
        long execs = 0;
        var tick = Stopwatch.StartNew();

        while (true)
        {
            if (tick.ElapsedMilliseconds >= 1000)
            {
                if (!ParentAlive(parentPid)) return 0;
                Send(new WorkerMessage
                {
                    Type = WorkerMessage.StatsType, Execs = execs, Hits = hits,
                    Metrics = source.Metrics?.ToDictionary(),
                });
                hits = new Dictionary<string, long>();
                tick.Restart();
            }

            if (source.Next(rand) is not { } next) continue;

            shared.Write(next.Input);
            for (int i = 0; i < parsers.Length; i++) outcomes[i] = parsers[i].Parse(next.Input);
            source.Feedback(parsers, outcomes);
            execs++;

            foreach (var signature in Findings.Signatures(parsers, outcomes))
            {
                hits[signature] = hits.GetValueOrDefault(signature) + 1;
                if (shortest.TryGetValue(signature, out var len) && len <= next.Input.Length) continue;

                shortest[signature] = next.Input.Length;
                Send(new WorkerMessage
                {
                    Type = WorkerMessage.FindingType,
                    Signature = signature,
                    InputBase64 = Encode.ToBase64(next.Input),
                    Origin = next.Origin,
                    Outcomes = parsers.Select((p, i) => $"{p.Name}: {Findings.Escape(outcomes[i].ToString())}").ToArray(),
                });
            }
        }
    }

    private static void Send(WorkerMessage message)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(message, Encode.Json));
        Console.Out.Flush();
    }

    private static bool ParentAlive(int pid)
    {
        if (pid < 0) return true;
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
