using System.Text.Json;
using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Clet;

/// <summary>
/// Persisted settings for the editor clet. Each property is discovered by
/// <see cref="ConfigurationManager"/> via <see cref="ConfigurationPropertyAttribute"/>
/// and is loaded automatically from <c>~/.tui/clet.config.json</c>.
/// </summary>
internal static partial class EditorSettings
{
    // --- View toggles ---

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool LineNumbers { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool FoldIndicators { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool WordWrap { get; set; }

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool ShowTabs { get; set; }

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool UseThemeBackground { get; set; }

    // --- Tab settings ---

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static int IndentSize { get; set; } = 4;

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool ConvertTabsToSpaces { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    public static bool AutoIndent { get; set; }

    /// <summary>
    /// All keys managed by this class. Used for selective persistence.
    /// </summary>
    private static readonly string[] _keys =
    [
        "EditorSettings.LineNumbers",
        "EditorSettings.FoldIndicators",
        "EditorSettings.WordWrap",
        "EditorSettings.ShowTabs",
        "EditorSettings.UseThemeBackground",
        "EditorSettings.IndentSize",
        "EditorSettings.ConvertTabsToSpaces",
        "EditorSettings.AutoIndent",
    ];

    /// <summary>
    /// Saves current property values to <c>~/.tui/clet.config.json</c>,
    /// preserving all JSONC content (comments, formatting, non-editor keys).
    /// After writing, reloads <see cref="ConfigurationManager"/> so that in-memory
    /// state matches the persisted file.
    /// </summary>
    internal static void Save () => Save (ConfigClet.GetConfigPath ());

    /// <summary>
    /// Saves current property values to the specified config file path,
    /// preserving all JSONC content (comments, formatting, non-editor keys).
    /// </summary>
    internal static void Save (string path)
    {
        ConfigClet.EnsureConfigFile (path);

        try
        {
            string text = File.ReadAllText (path);

            // Build key → JSON-value pairs for each managed setting.
            Dictionary<string, string> entries = new ()
            {
                ["EditorSettings.LineNumbers"] = ToJson (LineNumbers),
                ["EditorSettings.FoldIndicators"] = ToJson (FoldIndicators),
                ["EditorSettings.WordWrap"] = ToJson (WordWrap),
                ["EditorSettings.ShowTabs"] = ToJson (ShowTabs),
                ["EditorSettings.UseThemeBackground"] = ToJson (UseThemeBackground),
                ["EditorSettings.IndentSize"] = IndentSize.ToString (),
                ["EditorSettings.ConvertTabsToSpaces"] = ToJson (ConvertTabsToSpaces),
                ["EditorSettings.AutoIndent"] = ToJson (AutoIndent),
            };

            List<string> toInsert = [];

            foreach (KeyValuePair<string, string> kvp in entries)
            {
                // Try to replace an existing key in-place (preserves surrounding JSONC).
                // The regex matches: "key" : <value> on non-comment lines.
                string pattern = $@"(""{Regex.Escape (kvp.Key)}""\s*:\s*)(?:true|false|\d+)";

                if (Regex.IsMatch (text, pattern))
                {
                    text = Regex.Replace (text, pattern, $"${{1}}{kvp.Value}");
                }
                else
                {
                    toInsert.Add ($"  \"{kvp.Key}\": {kvp.Value}");
                }
            }

            // Insert new keys (not previously in the file) before the last closing '}'.
            if (toInsert.Count > 0)
            {
                int lastBrace = text.LastIndexOf ('}');

                if (lastBrace >= 0)
                {
                    // Find the position of the last non-whitespace, non-comment character
                    // before the closing brace so we can insert a comma after it.
                    int insertCommaAfter = FindLastJsonTokenPosition (text, lastBrace);

                    if (insertCommaAfter >= 0 && text[insertCommaAfter] != ',' && text[insertCommaAfter] != '{')
                    {
                        // Insert comma after the last JSON value
                        text = text.Insert (insertCommaAfter + 1, ",");

                        // Adjust lastBrace since we inserted a character
                        lastBrace = text.LastIndexOf ('}');
                    }

                    string insertion = $"\n\n{string.Join (",\n", toInsert)}\n";
                    text = text.Insert (lastBrace, insertion);
                }
            }

            File.WriteAllText (path, text);

            // Sync ConfigurationManager so in-memory state matches the file.
            if (ConfigurationManager.IsEnabled)
            {
                ConfigurationManager.Load (ConfigLocations.All);
                ConfigurationManager.Apply ();
            }
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Save: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the keys managed by this class. Useful for testing.
    /// </summary>
    internal static IReadOnlyList<string> ManagedKeys => _keys;

    /// <summary>Converts a boolean to its JSON literal.</summary>
    private static string ToJson (bool value) => value ? "true" : "false";

    /// <summary>
    /// Finds the position of the last non-whitespace, non-comment character
    /// before <paramref name="braceIndex"/>. This is where a trailing comma
    /// should be inserted when appending new properties.
    /// Returns -1 if only whitespace/comments precede the brace.
    /// </summary>
    private static int FindLastJsonTokenPosition (string text, int braceIndex)
    {
        int i = braceIndex - 1;

        while (i >= 0)
        {
            char c = text[i];

            if (char.IsWhiteSpace (c))
            {
                i--;

                continue;
            }

            // Check if we're at the end of a line comment.
            // Walk back to find if this line starts with "//".
            int lineStart = text.LastIndexOf ('\n', i) + 1;
            string line = text[lineStart..(i + 1)].TrimStart ();

            if (line.StartsWith ("//", StringComparison.Ordinal))
            {
                // This entire line is a comment — skip to before it.
                i = lineStart - 1;

                continue;
            }

            return i;
        }

        return -1;
    }
}
