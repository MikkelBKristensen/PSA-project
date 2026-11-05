using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fuzzers.Runner;

/// <summary>One line of JSON on a worker's stdout.</summary>
public sealed record WorkerMessage
{
    public const string FindingType = "finding";
    public const string StatsType = "stats";

    public required string Type { get; init; }

    // stats: inputs run since this worker process started, and hits per signature since the last stats message
    public long Execs { get; init; }
    public Dictionary<string, long>? Hits { get; init; }

    // finding: a new signature, or a shorter input for a known one
    public string? Signature { get; init; }
    public string? InputBase64 { get; init; }
    public string? Origin { get; init; }
    public string[]? Outcomes { get; init; }
}

/// <summary>A deduplicated finding as stored in <c>findings/&lt;id&gt;.json</c>.</summary>
public sealed class Finding
{
    public required string Id { get; init; }
    public required string Signature { get; init; }
    public required string Kind { get; init; }
    public long Hits { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public string? Strategy { get; set; }
    public string? Origin { get; set; }
    public int InputLength { get; set; }

    /// <summary>The exact input as UTF-16LE bytes, so lone surrogates etc. survive. Use this to reproduce.</summary>
    public string InputBase64 { get; set; } = "";

    /// <summary>Readable version of the input with awkward characters escaped.</summary>
    public string InputPreview { get; set; } = "";

    public string[] Outcomes { get; set; } = [];

    public static string IdFor(string signature) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..10];

    public string Input() => Encode.FromBase64(InputBase64);
}

public static class Encode
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    public static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true };

    public static string ToBase64(string s) => Convert.ToBase64String(Encoding.Unicode.GetBytes(s));
    public static string FromBase64(string b64) => Encoding.Unicode.GetString(Convert.FromBase64String(b64));
}
