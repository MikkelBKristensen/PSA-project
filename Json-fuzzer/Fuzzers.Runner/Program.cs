using System.Text.Json;
using Fuzzers.Harness;
using Fuzzers.Runner;

var options = Options.Parse(args.Skip(1));
return args.FirstOrDefault() switch
{
    "fuzz" => new Supervisor(options).Run(),
    "worker" => Worker.Run(options),
    "repro" => Repro(options),
    _ => Help(),
};

static int Repro(Options o)
{
    // Re-runs every parser on saved findings (findings/<id>.json) or plain text files.
    foreach (var path in o.Positional)
    {
        var input = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Deserialize<Finding>(File.ReadAllText(path))!.Input()
            : File.ReadAllText(path);

        Console.WriteLine($"{path}  ({input.Length} chars)");
        Console.WriteLine($"  input: {Findings.Escape(input.Length > 500 ? input[..500] + "..." : input)}");
        foreach (var parser in JsonParsers.All())
            Console.WriteLine($"  {parser.Name,-15} {Findings.Escape(parser.Parse(input).ToString())}");
    }

    return 0;
}

static int Help()
{
    Console.WriteLine($"""
        Usage:
          Fuzzers.Runner fuzz [options]      run the fuzzer
          Fuzzers.Runner repro <file>...     re-run all parsers on saved findings (.json) or text files

        fuzz options:
          --strategy <name>     {string.Join(" | ", InputSources.Names)}   (default grammar-mutation)
          --duration <time>     e.g. 30s, 90m, 8h, 0 = until Ctrl+C                 (default 1h)
          --workers <n>         parallel worker processes                           (default: CPU cores)
          --max-depth <n>       maximum object/array nesting                         (default 6)
          --seed <n>            base seed                                           (default random)
          --hang-timeout <s>    seconds without progress before a worker is a hang  (default 10)
          --out <dir>           output directory; findings in it are kept/extended  (default fuzz-out)
        """);
    return 1;
}
