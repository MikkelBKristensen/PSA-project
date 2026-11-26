namespace Fuzzers.Util;

public static class Alphabet
{
    public static readonly string[] PlainCharacters =
    [
        .. Enumerable.Range(0x20, 0x7F - 0x20).Select(c => ((char)c).ToString()).Where(c => c is not "\"" and not "\\"),
    "é", "€", "😀",
];
}
