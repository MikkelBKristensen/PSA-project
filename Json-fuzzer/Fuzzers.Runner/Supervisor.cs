using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Fuzzers.Harness;

namespace Fuzzers.Runner;

/// <summary>
/// Runs N worker processes for a fixed duration (or until Ctrl+C), merges and deduplicates their findings, and
/// keeps going when a worker dies or hangs: the input that killed it is saved and the worker is restarted.
/// </summary>
/// <remarks>
/// Output directory:
/// <list type="bullet">
/// <item><c>findings/&lt;id&gt;.json</c> one file per distinct finding, with the shortest input seen</item>
/// <item><c>findings/&lt;id&gt;.txt</c> the same input as UTF-8 (lossy for lone surrogates; the .json is exact)</item>
/// <item><c>findings.csv</c> summary of all findings</item>
/// <item><c>stats.csv</c> progress over time, one row per minute</item>
/// </list>
/// Findings from earlier runs in the same directory are loaded, so a run can be stopped and continued.
/// </remarks>
public sealed class Supervisor
{
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(30);

    private readonly string outDir, findingsDir, workDir, strategy;
    private readonly int workerCount, maxDepth, baseSeed;
    private readonly TimeSpan? duration;
    private readonly TimeSpan hangTimeout;
    private readonly Dictionary<string, Finding> findings = new();
    private readonly object gate = new();
    private readonly Slot[] slots;
    private volatile bool stopping;
    private long retiredExecs;
    private int restarts;
    private readonly Stopwatch clock = new();

    public Supervisor(Options o)
    {
        outDir = Path.GetFullPath(o.Get("out", "fuzz-out"));
        findingsDir = Path.Combine(outDir, "findings");
        workDir = Path.Combine(outDir, "work");
        strategy = o.Get("strategy", "grammar-mutation");
        workerCount = o.Int("workers", Environment.ProcessorCount);
        maxDepth = o.Int("max-depth", 6);
        baseSeed = o.Int("seed", Random.Shared.Next());
        duration = o.Duration("duration", "1h");
        hangTimeout = TimeSpan.FromSeconds(o.Int("hang-timeout", 10));
        slots = Enumerable.Range(0, workerCount).Select(i => new Slot(i)).ToArray();

        _ = InputSources.Create(strategy, maxDepth); // fail fast on a bad strategy name
    }

    public int Run()
    {
        Directory.CreateDirectory(findingsDir);
        Directory.CreateDirectory(workDir);
        LoadExistingFindings();

        Console.WriteLine($"Fuzzing with strategy '{strategy}', {workerCount} workers, max depth {maxDepth}, " +
                          $"seed {baseSeed}, duration {(duration?.ToString() ?? "until Ctrl+C")}");
        Console.WriteLine($"Output: {outDir} ({findings.Count} findings from earlier runs)");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stopping = true;
            Console.WriteLine("\nStopping...");
        };

        clock.Start();
        foreach (var slot in slots) Start(slot);

        var nextStatus = TimeSpan.Zero;
        var nextFlush = TimeSpan.FromMinutes(1);
        while (!stopping && (duration is null || clock.Elapsed < duration))
        {
            Thread.Sleep(500);
            foreach (var slot in slots) Check(slot);

            if (clock.Elapsed >= nextStatus)
            {
                PrintStatus();
                nextStatus = clock.Elapsed + TimeSpan.FromSeconds(10);
            }

            if (clock.Elapsed >= nextFlush)
            {
                Flush();
                nextFlush = clock.Elapsed + TimeSpan.FromMinutes(1);
            }
        }

        stopping = true;
        foreach (var slot in slots) Kill(slot);
        Flush();
        PrintStatus();
        PrintSummary();
        return 0;
    }

    private sealed class Slot(int index)
    {
        public int Index => index;
        public Process? Process;
        public SharedInput? Shared;
        public long Execs;          // reported by the current process
        public Dictionary<string, long>? Metrics;
        public long LastCounter;
        public TimeSpan LastProgress;
        public int Generation;
    }

    private void Start(Slot slot)
    {
        slot.Shared ??= SharedInput.Create(Path.Combine(workDir, $"worker-{slot.Index}.bin"));
        slot.LastCounter = slot.Shared.Counter;
        slot.LastProgress = clock.Elapsed + StartupGrace;
        slot.Execs = 0;
        slot.Metrics = null;

        int seed = HashCode.Combine(baseSeed, slot.Index, slot.Generation++);
        var psi = WorkerStartInfo();
        foreach (var arg in new[]
                 {
                     "worker", "--seed", seed.ToString(CultureInfo.InvariantCulture),
                     "--parent", Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                     "--shared", Path.Combine(workDir, $"worker-{slot.Index}.bin"),
                     "--strategy", strategy, "--max-depth", maxDepth.ToString(CultureInfo.InvariantCulture),
                 })
            psi.ArgumentList.Add(arg);

        var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) OnMessage(slot, process, e.Data); };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                File.AppendAllText(Path.Combine(workDir, $"worker-{slot.Index}.err"), e.Data + Environment.NewLine);
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        slot.Process = process;
    }

    private static ProcessStartInfo WorkerStartInfo()
    {
        var exe = Environment.ProcessPath!;
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        // Started as "dotnet Fuzzers.Runner.dll": pass the dll on.
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        return psi;
    }

    /// <summary>Detects dead and hanging workers, records the input responsible, and restarts them.</summary>
    private void Check(Slot slot)
    {
        var p = slot.Process;
        if (p is null || stopping) return;

        if (p.HasExited)
        {
            p.WaitForExit(); // drain stdout
            var input = slot.Shared!.Read();
            Record($"process-crash | exit code 0x{p.ExitCode:X8}", input,
                $"worker {slot.Index}: process exited; input is the last one it started", []);
            Retire(slot);
            Start(slot);
            return;
        }

        long counter = slot.Shared!.Counter;
        if (counter != slot.LastCounter)
        {
            slot.LastCounter = counter;
            slot.LastProgress = clock.Elapsed;
        }
        else if (clock.Elapsed - slot.LastProgress > hangTimeout)
        {
            var input = slot.Shared.Read();
            Kill(slot);
            Record("hang | no progress", input,
                $"worker {slot.Index}: no new input for {hangTimeout.TotalSeconds:0}s", []);
            Retire(slot);
            Start(slot);
        }
    }

    private void Retire(Slot slot)
    {
        Interlocked.Add(ref retiredExecs, slot.Execs);
        Interlocked.Increment(ref restarts);
        slot.Execs = 0;
        slot.Process?.Dispose();
        slot.Process = null;
    }

    private static void Kill(Slot slot)
    {
        try
        {
            if (slot.Process is { HasExited: false } p)
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // already gone
        }
    }

    private void OnMessage(Slot slot, Process sender, string line)
    {
        WorkerMessage? m;
        try
        {
            m = JsonSerializer.Deserialize<WorkerMessage>(line, Encode.Json);
        }
        catch (JsonException)
        {
            return; // stray output
        }

        if (m is null || !ReferenceEquals(slot.Process, sender) && m.Type == WorkerMessage.StatsType) return;

        if (m.Type == WorkerMessage.StatsType)
        {
            slot.Execs = m.Execs;
            slot.Metrics = m.Metrics;
            if (m.Hits is null) return;
            lock (gate)
            {
                foreach (var (sig, n) in m.Hits)
                    if (findings.TryGetValue(sig, out var f)) f.Hits += n;
            }
        }
        else if (m.Type == WorkerMessage.FindingType && m.Signature is not null && m.InputBase64 is not null)
        {
            Record(m.Signature, Encode.FromBase64(m.InputBase64), m.Origin, m.Outcomes ?? [], countHit: false);
        }
    }

    /// <summary>Adds a new finding, or replaces the stored input if this one is shorter.</summary>
    private void Record(string signature, string input, string? origin, string[] outcomes, bool countHit = true)
    {
        lock (gate)
        {
            bool isNew = !findings.TryGetValue(signature, out var f);
            if (isNew)
            {
                f = new Finding
                {
                    Id = Finding.IdFor(signature),
                    Signature = signature,
                    Kind = Findings.Kind(signature),
                    FirstSeenUtc = DateTime.UtcNow,
                    Strategy = strategy,
                };
                findings[signature] = f;
                Console.WriteLine($"[{Elapsed()}] NEW {f.Id} {signature}");
            }
            else if (input.Length >= f!.InputLength)
            {
                if (countHit) f.Hits++;
                return;
            }

            if (countHit) f!.Hits++;
            f!.Origin = origin;
            f.Outcomes = outcomes;
            f.InputLength = input.Length;
            f.InputBase64 = Encode.ToBase64(input);
            f.InputPreview = Findings.Escape(input.Length > 2000 ? input[..2000] + "..." : input);
            File.WriteAllText(Path.Combine(findingsDir, f.Id + ".json"), JsonSerializer.Serialize(f, Encode.JsonIndented));
            File.WriteAllText(Path.Combine(findingsDir, f.Id + ".txt"), input, new UTF8Encoding(false));
        }
    }

    private void LoadExistingFindings()
    {
        foreach (var file in Directory.EnumerateFiles(findingsDir, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<Finding>(File.ReadAllText(file)) is { } f) findings[f.Signature] = f;
            }
            catch (JsonException)
            {
                Console.WriteLine($"warning: could not read {file}");
            }
        }
    }

    private long TotalExecs() => Interlocked.Read(ref retiredExecs) + slots.Sum(s => s.Execs);

    /// <summary>Average of a worker metric over workers that report it (each worker has its own corpus).</summary>
    private long? AverageMetric(string name)
    {
        var values = slots.Select(s => s.Metrics).Where(m => m is not null && m.ContainsKey(name))
            .Select(m => m![name]).ToList();
        return values.Count == 0 ? null : (long)values.Average();
    }

    private string Elapsed() => clock.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    private void PrintStatus()
    {
        long execs = TotalExecs();
        double rate = execs / Math.Max(1, clock.Elapsed.TotalSeconds);
        int alive = slots.Count(s => s.Process is { HasExited: false });
        lock (gate)
        {
            var byKind = findings.Values.GroupBy(f => f.Kind).OrderBy(g => g.Key)
                .Select(g => $"{g.Key} {g.Count()}");
            var guidance = AverageMetric("corpus") is { } corpus
                ? $" | per worker: corpus {corpus:N0}, features {AverageMetric("features"):N0}"
                : "";
            Console.WriteLine($"[{Elapsed()}] {execs:N0} inputs, {rate:N0}/s, {alive}/{workerCount} workers, " +
                              $"{restarts} restarts | {findings.Count} findings: {string.Join(", ", byKind)}{guidance}");
        }
    }

    private void Flush()
    {
        long execs = TotalExecs();
        lock (gate)
        {
            var statsPath = Path.Combine(outDir, "stats.csv");
            if (!File.Exists(statsPath))
                File.WriteAllText(statsPath, "utc,strategy,elapsed_s,inputs,inputs_per_s,findings,crash,disagree,hang," +
                                             "process_crash,restarts,corpus_per_worker,features_per_worker\n");
            int Count(string kind) => findings.Values.Count(f => f.Kind == kind);
            File.AppendAllText(statsPath, string.Join(",",
                DateTime.UtcNow.ToString("O"), strategy, (long)clock.Elapsed.TotalSeconds, execs,
                (long)(execs / Math.Max(1, clock.Elapsed.TotalSeconds)), findings.Count,
                Count("crash"), Count("disagree"), Count("hang"), Count("process-crash"), restarts,
                AverageMetric("corpus"), AverageMetric("features")) + "\n");

            var csv = new StringBuilder("id,kind,hits,input_length,first_seen_utc,strategy,signature,origin\n");
            foreach (var f in findings.Values.OrderBy(f => f.Kind).ThenByDescending(f => f.Hits))
            {
                csv.AppendJoin(",", f.Id, f.Kind, f.Hits, f.InputLength, f.FirstSeenUtc.ToString("O"), f.Strategy,
                    Csv(f.Signature), Csv(f.Origin)).Append('\n');
            }

            File.WriteAllText(Path.Combine(outDir, "findings.csv"), csv.ToString(), new UTF8Encoding(false));
        }
    }

    private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    private void PrintSummary()
    {
        Console.WriteLine();
        lock (gate)
        {
            foreach (var g in findings.Values.GroupBy(f => f.Kind).OrderBy(g => g.Key))
            {
                Console.WriteLine($"{g.Key} ({g.Count()}):");
                foreach (var f in g.OrderByDescending(f => f.Hits).Take(15))
                    Console.WriteLine($"  {f.Id} {f.Hits,10:N0} hits  {f.Signature}");
            }
        }

        Console.WriteLine($"\nDetails: {Path.Combine(outDir, "findings.csv")} and {findingsDir}");
    }
}
