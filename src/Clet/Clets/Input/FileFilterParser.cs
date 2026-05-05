namespace Clet;

internal static class FileFilterParser
{
    public static string[] ParseExtensions (string? filter)
    {
        if (string.IsNullOrWhiteSpace (filter))
        {
            return [];
        }

        return filter
            .Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select (Normalize)
            .Where (e => e.Length > 1)
            .Distinct (StringComparer.OrdinalIgnoreCase)
            .ToArray ();
    }

    private static string Normalize (string token)
    {
        string s = token.TrimStart ('*');

        return s.StartsWith ('.') ? s : "." + s;
    }
}
