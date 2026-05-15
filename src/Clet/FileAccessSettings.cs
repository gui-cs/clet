using System.Text.Json.Serialization;
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
    [JsonConverter (typeof (StringArrayJsonConverter))]
    public static string[] AllowedPaths { get; set; } = [];
}
