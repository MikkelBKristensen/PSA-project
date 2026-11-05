using System.Globalization;

namespace Fuzzers.Runner;

/// <summary>Minimal <c>--key value</c> / <c>--flag</c> argument parser.</summary>
public sealed class Options
{
    private readonly Dictionary<string, string?> values = new();
    public List<string> Positional { get; } = new();

    public static Options Parse(IEnumerable<string> args)
    {
        var o = new Options();
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--")) { o.Positional.Add(list[i]); continue; }
            var key = list[i][2..];
            bool hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
            o.values[key] = hasValue ? list[++i] : null;
        }

        return o;
    }

    public bool Flag(string key) => values.ContainsKey(key);

    public string Get(string key, string fallback) => values.TryGetValue(key, out var v) && v is not null ? v : fallback;

    public int Int(string key, int fallback) => int.Parse(Get(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    /// <summary>Accepts "8h", "90m", "30s", "1d", a TimeSpan ("01:30:00"), or "0" for no limit.</summary>
    public TimeSpan? Duration(string key, string fallback)
    {
        var s = Get(key, fallback).Trim().ToLowerInvariant();
        if (s is "0" or "inf" or "forever") return null;
        if (s.Length > 1 && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            switch (s[^1])
            {
                case 's': return TimeSpan.FromSeconds(n);
                case 'm': return TimeSpan.FromMinutes(n);
                case 'h': return TimeSpan.FromHours(n);
                case 'd': return TimeSpan.FromDays(n);
            }
        }

        return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
    }
}
