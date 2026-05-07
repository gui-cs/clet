using System.Text.Json.Nodes;

namespace Clet;

/// <summary>
/// Canonical CLR-type → wire-name mapping for the clet registry.
/// Single source of truth for the wire-name vocabulary that is locked at v0.5 schema-lock.
/// </summary>
internal static class CletTypeNames
{
    /// <summary>
    /// Maps a CLR <see cref="Type"/> to its wire-format name used in <c>clet list --json</c>
    /// and Markdown help output.
    /// </summary>
    public static string WireName (Type type)
    {
        Type underlying = Nullable.GetUnderlyingType (type) ?? type;

        if (underlying == typeof (string))
        {
            return "string";
        }

        if (underlying == typeof (int) || underlying == typeof (long) || underlying == typeof (short))
        {
            return "int";
        }

        if (underlying == typeof (decimal) || underlying == typeof (double) || underlying == typeof (float))
        {
            return "decimal";
        }

        if (underlying == typeof (bool))
        {
            return "bool";
        }

        if (underlying == typeof (DateTime) || underlying == typeof (DateOnly))
        {
            return "date";
        }

        if (underlying == typeof (TimeOnly))
        {
            return "time";
        }

        if (underlying == typeof (TimeSpan))
        {
            return "duration";
        }

        if (underlying == typeof (JsonArray))
        {
            return "array";
        }

        if (underlying == typeof (JsonObject))
        {
            return "object";
        }

        if (underlying == typeof (JsonNode))
        {
            return "json";
        }

        if (underlying == typeof (void))
        {
            return "none";
        }

        return underlying.Name;
    }
}
