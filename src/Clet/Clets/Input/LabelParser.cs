namespace Clet;

internal static class LabelParser
{
    public static string[] Split (IEnumerable<string> args) =>
        Split (string.Join (',', args));

    public static string[] Split (string? joined) =>
        joined is null
            ? []
            : joined.Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
