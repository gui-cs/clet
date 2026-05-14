using System.Text.Json;
using System.Text.Json.Nodes;

namespace Clet;

/// <summary>
/// Persisted settings for the editor clet. Stored under the <c>"clet.edit"</c> key
/// in <c>~/.tui/clet.config.json</c>.
/// </summary>
internal sealed class EditorSettings
{
    /// <summary>The key used in the config JSON file.</summary>
    private const string ConfigKey = "clet.edit";

    // --- View toggles ---

    public bool LineNumbers { get; set; } = true;
    public bool FoldIndicators { get; set; } = true;
    public bool WordWrap { get; set; }
    public bool ShowTabs { get; set; }
    public bool UseThemeBackground { get; set; }

    // --- Tab settings ---

    public int IndentSize { get; set; } = 4;
    public bool ConvertTabsToSpaces { get; set; } = true;
    public bool AutoIndent { get; set; }

    /// <summary>
    /// Loads editor settings from <c>~/.tui/clet.config.json</c>.
    /// Returns defaults if the file or section doesn't exist.
    /// </summary>
    internal static EditorSettings Load ()
    {
        string path = ConfigClet.GetConfigPath ();

        if (!File.Exists (path))
        {
            return new ();
        }

        try
        {
            string json = File.ReadAllText (path);
            JsonNode? root = JsonNode.Parse (json, documentOptions: new () { CommentHandling = JsonCommentHandling.Skip });

            if (root is JsonObject obj && obj.TryGetPropertyValue (ConfigKey, out JsonNode? section) && section is not null)
            {
                EditorSettings? settings = section.Deserialize (EditorSettingsJsonContext.Default.EditorSettings);

                return settings ?? new ();
            }
        }
        catch
        {
            // If the file is malformed, fall back to defaults.
        }

        return new ();
    }

    /// <summary>
    /// Saves the current settings to <c>~/.tui/clet.config.json</c>,
    /// preserving all other keys in the file.
    /// </summary>
    internal void Save ()
    {
        string path = ConfigClet.GetConfigPath ();
        ConfigClet.EnsureConfigFile (path);

        try
        {
            string existing = File.ReadAllText (path);
            JsonNode? root = JsonNode.Parse (existing, documentOptions: new () { CommentHandling = JsonCommentHandling.Skip });
            JsonObject obj = root as JsonObject ?? new ();

            // Serialize our settings to a JsonNode and merge into the root object.
            JsonNode? settingsNode = JsonSerializer.SerializeToNode (this, EditorSettingsJsonContext.Default.EditorSettings);
            obj[ConfigKey] = settingsNode;

            JsonSerializerOptions writeOptions = new () { WriteIndented = true };
            string output = obj.ToJsonString (writeOptions);
            File.WriteAllText (path, output);
        }
        catch
        {
            // Best-effort persistence — don't crash the editor if the config file is locked, etc.
        }
    }
}
