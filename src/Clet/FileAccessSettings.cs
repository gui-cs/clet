using System.Text.Json;
using Terminal.Gui.App;

namespace Clet;

/// <summary>
/// Persistent file-access settings for <c>clet edit</c> and <c>clet md</c>.
/// Loaded from <c>"FileAccessSettings.AllowedPaths"</c> in <c>~/.tui/clet.config.json</c>
/// by calling <see cref="LoadFromConfig()"/> at application startup.
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
    /// Populated from <c>~/.tui/clet.config.json</c> by <see cref="LoadFromConfig()"/>.
    /// Set in the config file as:
    /// <code>
    /// "FileAccessSettings.AllowedPaths": ["/home/user/projects", "/tmp/docs"]
    /// </code>
    /// Files or directories listed here bypass extension and working-directory
    /// confinement checks (size and binary checks still apply).
    /// </remarks>
    internal static string[]? AllowedPaths { get; set; }

    /// <summary>
    /// Reads <see cref="AllowedPaths"/> from the clet config file at the
    /// default location (<c>~/.tui/clet.config.json</c>).
    /// Should be called during application startup after
    /// <see cref="Terminal.Gui.Configuration.ConfigurationManager"/> is enabled.
    /// </summary>
    internal static void LoadFromConfig () => LoadFromConfig (ConfigClet.GetConfigPath ());

    /// <summary>
    /// Reads <see cref="AllowedPaths"/> from the specified config file path.
    /// </summary>
    internal static void LoadFromConfig (string configPath)
    {
        AllowedPaths = null;

        if (!File.Exists (configPath))
        {
            return;
        }

        try
        {
            string text = File.ReadAllText (configPath);

            using JsonDocument doc = JsonDocument.Parse (
                text,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

            if (!doc.RootElement.TryGetProperty ("FileAccessSettings.AllowedPaths", out JsonElement el)
                || el.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            List<string> paths = [];

            foreach (JsonElement item in el.EnumerateArray ())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? val = item.GetString ();

                    if (!string.IsNullOrWhiteSpace (val))
                    {
                        paths.Add (val);
                    }
                }
            }

            AllowedPaths = paths.Count > 0 ? [.. paths] : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Non-fatal: silently ignore config read errors;
            // the policy will fall back to no config-based paths.
            Logging.Error ($"FileAccessSettings.LoadFromConfig: {ex.GetType ().Name}: {ex.Message}");
        }
    }
}
