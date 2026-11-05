using System.IO.MemoryMappedFiles;

namespace Fuzzers.Runner;

/// <summary>
/// A small memory-mapped file shared by a worker and the supervisor. Before every parse the worker writes the
/// input here and bumps a counter. If the worker dies (e.g. stack overflow) the supervisor can still read the
/// input that killed it; if the counter stops moving the worker is hanging.
/// </summary>
/// <remarks>Layout: [0..8) counter, [8..12) length in chars, [12..) UTF-16 chars.</remarks>
public sealed class SharedInput : IDisposable
{
    private const int HeaderBytes = 12;
    public const int MaxChars = 1 << 20;
    private const long Size = HeaderBytes + 2L * MaxChars;

    private readonly MemoryMappedFile file;
    private readonly MemoryMappedViewAccessor view;
    private long counter;

    private SharedInput(string path, FileMode mode)
    {
        var stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < Size) stream.SetLength(Size);
        file = MemoryMappedFile.CreateFromFile(stream, null, Size, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, leaveOpen: false);
        view = file.CreateViewAccessor(0, Size);
        counter = view.ReadInt64(0);
    }

    public static SharedInput Create(string path) => new(path, FileMode.OpenOrCreate);

    public static SharedInput Open(string path) => new(path, FileMode.Open);

    /// <summary>Number of inputs written so far.</summary>
    public long Counter => view.ReadInt64(0);

    public void Write(string input)
    {
        int n = Math.Min(input.Length, MaxChars);
        view.Write(8, n);
        view.WriteArray(HeaderBytes, input.ToCharArray(0, n), 0, n);
        view.Write(0, ++counter);
    }

    public string Read()
    {
        int n = Math.Clamp(view.ReadInt32(8), 0, MaxChars);
        var chars = new char[n];
        view.ReadArray(HeaderBytes, chars, 0, n);
        return new string(chars);
    }

    public void Dispose()
    {
        view.Dispose();
        file.Dispose();
    }
}
