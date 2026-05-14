using System.Text.Json;
using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Clet;

/// <summary>
/// Persisted settings for the editor clet. Each property is discovered by
/// <see cref="ConfigurationManager"/> via <see cref="ConfigurationPropertyAttribute"/>
/// and is loaded automatically from <c>~/.tui/clet.config.json</c>.
/// </summary>
internal static class EditorSettings
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
    /// preserving all other keys in the file.
    /// </summary>
    internal static void Save () => Save (ConfigClet.GetConfigPath ());

    /// <summary>
    /// Saves current property values to the specified config file path,
    /// preserving all other keys in the file.
    /// </summary>
    internal static void Save (string path)
    {
        ConfigClet.EnsureConfigFile (path);

        try
        {
            string existing = File.ReadAllText (path);

            JsonNode? root = JsonNode.Parse (
                existing,
                documentOptions: new ()
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            JsonObject obj = root as JsonObject ?? new ();

            // Write each managed key into the config JSON
            obj["EditorSettings.LineNumbers"] = LineNumbers;
            obj["EditorSettings.FoldIndicators"] = FoldIndicators;
            obj["EditorSettings.WordWrap"] = WordWrap;
            obj["EditorSettings.ShowTabs"] = ShowTabs;
            obj["EditorSettings.UseThemeBackground"] = UseThemeBackground;
            obj["EditorSettings.IndentSize"] = IndentSize;
            obj["EditorSettings.ConvertTabsToSpaces"] = ConvertTabsToSpaces;
            obj["EditorSettings.AutoIndent"] = AutoIndent;

            JsonSerializerOptions writeOptions = new () { WriteIndented = true };
            File.WriteAllText (path, obj.ToJsonString (writeOptions));
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
}
