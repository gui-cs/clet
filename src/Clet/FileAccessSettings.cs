using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Clet;

/// <summary>
/// Persistent file-access settings for <c>clet edit</c> and <c>clet md</c>.
/// Properties are discovered by <see cref="ConfigurationManager"/> and loaded
/// automatically from <c>~/.tui/clet.config.json</c>.
///
/// <para>Add directory paths to <see cref="AllowedPaths"/> to grant
/// permanent access without requiring <c>--allow-file</c> each time.</para>
/// </summary>
internal static class FileAccessSettings
{
    /// <summary>
    /// Directories (or files) that are permanently allowed for <c>clet edit</c>
    /// and <c>clet md</c>, regardless of the working directory.
    /// Equivalent to VS Code's trusted-folders list.
    /// </summary>
    /// <remarks>
    /// Set in <c>~/.tui/clet.config.json</c> as:
    /// <code>
    /// "FileAccessSettings.AllowedPaths": ["/home/user/projects", "/tmp/docs"]
    /// </code>
    /// Files or directories listed here bypass extension and working-directory
    /// confinement checks (size and binary checks still apply).
    /// </remarks>
    [ConfigurationProperty (Scope = typeof (SettingsScope))]
    [System.Text.Json.Serialization.JsonConverter (typeof (StringArrayJsonConverter))]
    public static string[] AllowedPaths { get; set; } = [];

    /// <summary>
    /// Adds <paramref name="dirPath"/> to <see cref="AllowedPaths"/> both in memory
    /// and persistently in <c>~/.tui/clet.config.json</c>, then reloads
    /// <see cref="ConfigurationManager"/> so the change takes effect immediately.
    /// </summary>
    /// <param name="dirPath">The directory (or file) path to trust.</param>
    internal static void AddToConfig (string dirPath) => AddToConfig (dirPath, ConfigClet.GetConfigPath ());

    /// <summary>
    /// Adds <paramref name="dirPath"/> to the persistent allow list stored in
    /// <paramref name="configPath"/>.  Separated from <see cref="AddToConfig(string)"/>
    /// for testability.
    /// </summary>
    [UnconditionalSuppressMessage ("Trimming", "IL2026",
        Justification = "Operates only on string values, which are primitive JSON types and trim-safe.")]
    [UnconditionalSuppressMessage ("AOT", "IL3050",
        Justification = "Operates only on string values, which are primitive JSON types and AOT-safe.")]
    internal static void AddToConfig (string dirPath, string configPath)
    {
        ConfigClet.EnsureConfigFile (configPath);

        try
        {
            string text = File.ReadAllText (configPath);

            // Parse the JSONC file — comments are stripped, but all active keys
            // (EditorSettings.*, etc.) are preserved in the rewritten file.
            JsonNode? root = JsonNode.Parse (
                text,
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            if (root is not JsonObject obj)
            {
                return;
            }

            // Append to existing array or create a new one.
            // Implicit string → JsonNode cast avoids reflection (IL2026/IL3050).
            JsonNode dirPathNode = (JsonNode) dirPath;

            if (obj ["FileAccessSettings.AllowedPaths"] is JsonArray existing)
            {
                bool found = existing.Any (n => n?.GetValue<string> () == dirPath);

                if (!found)
                {
                    existing.Add (dirPathNode);
                }
            }
            else
            {
                obj ["FileAccessSettings.AllowedPaths"] = new JsonArray (dirPathNode);
            }

            File.WriteAllText (configPath, obj.ToJsonString (new JsonSerializerOptions { WriteIndented = true }));

            // Sync ConfigurationManager so subsequent Enable/Apply also see the change.
            if (ConfigurationManager.IsEnabled)
            {
                ConfigurationManager.Load (ConfigLocations.All);
                ConfigurationManager.Apply ();
            }

            // Ensure the in-memory property contains dirPath regardless of whether
            // CM reset it (e.g. when configPath differs from CM's active config).
            if (!AllowedPaths.Contains (dirPath))
            {
                AllowedPaths = [.. AllowedPaths, dirPath];
            }
        }
        catch (Exception ex)
        {
            Logging.Error ($"FileAccessSettings.AddToConfig: {ex.GetType ().Name}: {ex.Message}");
        }
    }
}
