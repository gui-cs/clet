using System.Text.Json;
using System.Text.Json.Nodes;
using Terminal.Gui.Configuration;
using Xunit;

namespace Clet.UnitTests;

/// <summary>
/// Defines a non-parallel collection for tests that interact with
/// <see cref="ConfigurationManager"/>, which uses global static state.
/// </summary>
[CollectionDefinition (nameof (ConfigurationManagerCollection), DisableParallelization = true)]
public class ConfigurationManagerCollection;

/// <summary>
/// Tests for <see cref="EditorSettings"/> round-tripping through
/// <see cref="ConfigurationManager"/>.
/// </summary>
[Collection (nameof (ConfigurationManagerCollection))]
public class EditorSettingsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string? _originalHome;

    public EditorSettingsTests ()
    {
        _tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tuiDir = Path.Combine (_tempDir, ".tui");
        Directory.CreateDirectory (tuiDir);
        _configPath = Path.Combine (tuiDir, ConfigClet.ConfigFileName);

        // Save original HOME so we can restore it on cleanup.
        _originalHome = Environment.GetEnvironmentVariable ("HOME");

        // Point HOME at our temp directory (used by Save's CM reload on Linux).
        Environment.SetEnvironmentVariable ("HOME", _tempDir);

        // Ensure CM uses the "clet" app name (matches the clet binary; in tests
        // the assembly name is different).
        ConfigurationManager.AppName = "clet";
    }

    public void Dispose ()
    {
        // Reset CM so other tests start clean.
        try
        {
            ConfigurationManager.Disable (resetToHardCodedDefaults: true);
        }
        catch
        {
            // Best-effort cleanup.
        }

        // Restore original HOME.
        Environment.SetEnvironmentVariable ("HOME", _originalHome);

        if (Directory.Exists (_tempDir))
        {
            Directory.Delete (_tempDir, true);
        }
    }

    [Fact]
    public void ManagedKeys_ContainsAllProperties ()
    {
        Assert.Contains ("EditorSettings.LineNumbers", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.FoldIndicators", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.WordWrap", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.ShowTabs", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.UseThemeBackground", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.IndentSize", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.ConvertTabsToSpaces", EditorSettings.ManagedKeys);
        Assert.Contains ("EditorSettings.AutoIndent", EditorSettings.ManagedKeys);
        Assert.Equal (8, EditorSettings.ManagedKeys.Count);
    }

    [Fact]
    public void ConfigurationManager_Discovers_EditorSettings ()
    {
        ConfigurationManager.Enable (ConfigLocations.None);

        foreach (string key in EditorSettings.ManagedKeys)
        {
            Assert.True (
                ConfigurationManager.Settings!.Keys.Contains (key),
                $"ConfigurationManager.Settings should contain '{key}'");
        }
    }

    [Fact]
    public void Save_WritesAllKeys_ToConfigFile ()
    {
        // Arrange — set known values
        EditorSettings.LineNumbers = false;
        EditorSettings.FoldIndicators = false;
        EditorSettings.WordWrap = true;
        EditorSettings.ShowTabs = true;
        EditorSettings.UseThemeBackground = true;
        EditorSettings.IndentSize = 2;
        EditorSettings.ConvertTabsToSpaces = false;
        EditorSettings.AutoIndent = true;

        // Write a minimal config file so Save can insert into it
        File.WriteAllText (_configPath, "{}");

        // Act
        EditorSettings.Save (_configPath);

        // Assert — parse the written file and check values
        string json = File.ReadAllText (_configPath);

        JsonNode? root = JsonNode.Parse (
            json,
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        Assert.NotNull (root);
        JsonObject obj = Assert.IsType<JsonObject> (root);

        Assert.False ((bool)obj["EditorSettings.LineNumbers"]!);
        Assert.False ((bool)obj["EditorSettings.FoldIndicators"]!);
        Assert.True ((bool)obj["EditorSettings.WordWrap"]!);
        Assert.True ((bool)obj["EditorSettings.ShowTabs"]!);
        Assert.True ((bool)obj["EditorSettings.UseThemeBackground"]!);
        Assert.Equal (2, (int)obj["EditorSettings.IndentSize"]!);
        Assert.False ((bool)obj["EditorSettings.ConvertTabsToSpaces"]!);
        Assert.True ((bool)obj["EditorSettings.AutoIndent"]!);
    }

    [Fact]
    public void Save_PreservesExistingKeys ()
    {
        // Arrange
        File.WriteAllText (
            _configPath,
            """
            {
              "$schema": "https://example.com/schema.json",
              "Theme": "Dark"
            }
            """);

        EditorSettings.LineNumbers = true;

        // Act
        EditorSettings.Save (_configPath);

        // Assert
        string json = File.ReadAllText (_configPath);

        JsonNode? root = JsonNode.Parse (
            json,
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        Assert.NotNull (root);
        JsonObject obj = Assert.IsType<JsonObject> (root);

        Assert.Equal ("https://example.com/schema.json", (string)obj["$schema"]!);
        Assert.Equal ("Dark", (string)obj["Theme"]!);
        Assert.True ((bool)obj["EditorSettings.LineNumbers"]!);
    }

    [Fact]
    public void Save_PreservesJsoncComments ()
    {
        // Arrange — write a JSONC file with comments
        string jsonc =
            """
            {
              // This is a comment
              "$schema": "https://example.com/schema.json",

              // Theme configuration
              // "Theme": "Anders",

              "Key.Separator": "+"
            }
            """;
        File.WriteAllText (_configPath, jsonc);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 2;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — JSONC comments and existing keys are preserved
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// This is a comment", result);
        Assert.Contains ("// Theme configuration", result);
        Assert.Contains ("// \"Theme\": \"Anders\",", result);
        Assert.Contains ("\"Key.Separator\": \"+\"", result);
        Assert.Contains ("\"$schema\": \"https://example.com/schema.json\"", result);
        Assert.Contains ("\"EditorSettings.LineNumbers\": false", result);
        Assert.Contains ("\"EditorSettings.IndentSize\": 2", result);
    }

    [Fact]
    public void Save_PreservesDefaultConfigContent ()
    {
        // Arrange — use the full ConfigClet default JSONC template
        File.WriteAllText (_configPath, ConfigClet.DefaultConfigContent);

        EditorSettings.LineNumbers = false;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — the original JSONC structure is intact
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("clet configuration", result);
        Assert.Contains ("Terminal.Gui's ConfigurationManager", result);
        Assert.Contains ("$schema", result);
        Assert.Contains ("\"EditorSettings.LineNumbers\": false", result);
    }

    [Fact]
    public void Save_UpdatesExistingEditorSettingsKeys ()
    {
        // Arrange — write a file that already has EditorSettings keys
        File.WriteAllText (
            _configPath,
            """
            {
              // comments
              "EditorSettings.LineNumbers": true,
              "EditorSettings.IndentSize": 4
            }
            """);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 8;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — existing keys are updated in place
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// comments", result);
        Assert.Contains ("\"EditorSettings.LineNumbers\": false", result);
        Assert.Contains ("\"EditorSettings.IndentSize\": 8", result);

        // Verify no duplicate keys
        Assert.Equal (1, CountOccurrences (result, "EditorSettings.LineNumbers"));
        Assert.Equal (1, CountOccurrences (result, "EditorSettings.IndentSize"));
    }

    [Fact]
    public void Save_DoesNotModifyCommentedOutKeys ()
    {
        // Arrange — file has a commented-out EditorSettings key
        File.WriteAllText (
            _configPath,
            """
            {
              // "EditorSettings.LineNumbers": true,
              "EditorSettings.IndentSize": 4
            }
            """);

        EditorSettings.LineNumbers = false;
        EditorSettings.IndentSize = 2;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — commented-out key is untouched, active key is updated
        string result = File.ReadAllText (_configPath);

        Assert.Contains ("// \"EditorSettings.LineNumbers\": true,", result);
        Assert.Contains ("\"EditorSettings.IndentSize\": 2", result);
        Assert.Contains ("\"EditorSettings.LineNumbers\": false", result);
    }

    [Fact]
    public void RoundTrip_LoadApply_RestoresPersistedValues ()
    {
        // Ensure CM starts disabled so Enable(Runtime) is not a no-op on any platform.
        ConfigurationManager.Disable (resetToHardCodedDefaults: true);

        // Arrange — JSON with non-default values
        string json = """
            {
              "EditorSettings.LineNumbers": false,
              "EditorSettings.IndentSize": 8,
              "EditorSettings.WordWrap": true,
              "EditorSettings.AutoIndent": true
            }
            """;

        // Reset to defaults first
        EditorSettings.LineNumbers = true;
        EditorSettings.IndentSize = 4;
        EditorSettings.WordWrap = false;
        EditorSettings.AutoIndent = false;

        // Act — load via RuntimeConfig (cross-platform; avoids ~ resolution
        // issues on Windows where GetFolderPath ignores env var changes).
        ConfigurationManager.RuntimeConfig = json;
        ConfigurationManager.Enable (ConfigLocations.Runtime);

        // Assert
        Assert.False (EditorSettings.LineNumbers);
        Assert.Equal (8, EditorSettings.IndentSize);
        Assert.True (EditorSettings.WordWrap);
        Assert.True (EditorSettings.AutoIndent);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_RestoresValues ()
    {
        // Ensure CM starts disabled so Enable(Runtime) is not a no-op on any platform.
        ConfigurationManager.Disable (resetToHardCodedDefaults: true);

        // Arrange — write initial config, set non-default values, save
        File.WriteAllText (_configPath, "{}");

        EditorSettings.LineNumbers = false;
        EditorSettings.FoldIndicators = false;
        EditorSettings.IndentSize = 3;
        EditorSettings.ConvertTabsToSpaces = false;
        EditorSettings.Save (_configPath);

        // Reset in-memory to defaults
        EditorSettings.LineNumbers = true;
        EditorSettings.FoldIndicators = true;
        EditorSettings.IndentSize = 4;
        EditorSettings.ConvertTabsToSpaces = true;

        // Act — load saved file via RuntimeConfig (cross-platform).
        string savedJson = File.ReadAllText (_configPath);
        ConfigurationManager.RuntimeConfig = savedJson;
        ConfigurationManager.Enable (ConfigLocations.Runtime);

        // Assert — values should match what we saved
        Assert.False (EditorSettings.LineNumbers);
        Assert.False (EditorSettings.FoldIndicators);
        Assert.Equal (3, EditorSettings.IndentSize);
        Assert.False (EditorSettings.ConvertTabsToSpaces);
    }

    [Fact]
    public void Save_CreatesConfigFile_WhenMissing ()
    {
        // Arrange — no config file exists yet
        Assert.False (File.Exists (_configPath));

        EditorSettings.IndentSize = 6;

        // Act
        EditorSettings.Save (_configPath);

        // Assert — file was created and contains the setting
        Assert.True (File.Exists (_configPath));

        string json = File.ReadAllText (_configPath);

        JsonNode? root = JsonNode.Parse (
            json,
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        Assert.NotNull (root);
        Assert.Equal (6, (int)root!["EditorSettings.IndentSize"]!);
    }

    [Fact]
    public void Defaults_AreCorrect ()
    {
        // Reset CM so properties are at hard-coded defaults
        ConfigurationManager.Disable (resetToHardCodedDefaults: true);
        ConfigurationManager.Enable (ConfigLocations.None);
        ConfigurationManager.Load (ConfigLocations.HardCoded);
        ConfigurationManager.Apply ();

        Assert.True (EditorSettings.LineNumbers);
        Assert.True (EditorSettings.FoldIndicators);
        Assert.False (EditorSettings.WordWrap);
        Assert.False (EditorSettings.ShowTabs);
        Assert.False (EditorSettings.UseThemeBackground);
        Assert.Equal (4, EditorSettings.IndentSize);
        Assert.True (EditorSettings.ConvertTabsToSpaces);
        Assert.False (EditorSettings.AutoIndent);
    }

    [Fact]
    public void AllowedPaths_RoundTrip_CM_LoadsArrayFromRuntimeConfig ()
    {
        // Ensure CM starts disabled so Enable(Runtime) is not a no-op on any platform.
        ConfigurationManager.Disable (resetToHardCodedDefaults: true);

        // Arrange — reset in-memory value to empty
        string[] savedPaths = FileAccessSettings.AllowedPaths;
        FileAccessSettings.AllowedPaths = [];

        string json = """
            {
              "FileAccessSettings.AllowedPaths": ["/allowed/path1", "/allowed/path2"]
            }
            """;

        try
        {
            // Act — load via RuntimeConfig
            ConfigurationManager.RuntimeConfig = json;
            ConfigurationManager.Enable (ConfigLocations.Runtime);

            // Assert — CM must have applied the array
            Assert.NotEmpty (FileAccessSettings.AllowedPaths);
            Assert.Contains ("/allowed/path1", FileAccessSettings.AllowedPaths);
            Assert.Contains ("/allowed/path2", FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = savedPaths;
        }
    }

    /// <summary>Counts the number of occurrences of <paramref name="substring"/> in <paramref name="text"/>.</summary>
    private static int CountOccurrences (string text, string substring)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf (substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
