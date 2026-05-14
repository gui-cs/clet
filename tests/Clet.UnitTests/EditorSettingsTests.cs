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

    public EditorSettingsTests ()
    {
        _tempDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}");
        string tuiDir = Path.Combine (_tempDir, ".tui");
        Directory.CreateDirectory (tuiDir);
        _configPath = Path.Combine (tuiDir, ConfigClet.ConfigFileName);

        // Point HOME at our temp directory so CM picks up our test config file.
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

        // Restore HOME.
        Environment.SetEnvironmentVariable ("HOME", null);

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

        // Write a minimal config file so Save can merge into it
        File.WriteAllText (_configPath, "{}");

        // Act
        EditorSettings.Save (_configPath);

        // Assert
        string json = File.ReadAllText (_configPath);
        JsonNode? root = JsonNode.Parse (json);
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
        JsonNode? root = JsonNode.Parse (json);
        Assert.NotNull (root);
        JsonObject obj = Assert.IsType<JsonObject> (root);

        Assert.Equal ("https://example.com/schema.json", (string)obj["$schema"]!);
        Assert.Equal ("Dark", (string)obj["Theme"]!);
        Assert.True ((bool)obj["EditorSettings.LineNumbers"]!);
    }

    [Fact]
    public void RoundTrip_LoadApply_RestoresPersistedValues ()
    {
        // Arrange — write a config file with non-default values
        File.WriteAllText (
            _configPath,
            """
            {
              "EditorSettings.LineNumbers": false,
              "EditorSettings.IndentSize": 8,
              "EditorSettings.WordWrap": true,
              "EditorSettings.AutoIndent": true
            }
            """);

        // Reset to defaults first
        EditorSettings.LineNumbers = true;
        EditorSettings.IndentSize = 4;
        EditorSettings.WordWrap = false;
        EditorSettings.AutoIndent = false;

        // Act — enable CM and load + apply
        ConfigurationManager.Enable (ConfigLocations.All);
        ConfigurationManager.Load (ConfigLocations.All);
        ConfigurationManager.Apply ();

        // Assert
        Assert.False (EditorSettings.LineNumbers);
        Assert.Equal (8, EditorSettings.IndentSize);
        Assert.True (EditorSettings.WordWrap);
        Assert.True (EditorSettings.AutoIndent);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_RestoresValues ()
    {
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

        // Act — load + apply via CM
        ConfigurationManager.Enable (ConfigLocations.All);
        ConfigurationManager.Load (ConfigLocations.All);
        ConfigurationManager.Apply ();

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

        // Assert — file was created (by EnsureConfigFile + overwritten by Save)
        Assert.True (File.Exists (_configPath));

        string json = File.ReadAllText (_configPath);

        // Save writes pure JSON (JSONC comments/trailing commas are stripped during round-trip)
        JsonNode? root = JsonNode.Parse (json);

        Assert.NotNull (root);
        Assert.Equal (6, (int)root!["EditorSettings.IndentSize"]!);
    }

    [Fact]
    public void Defaults_AreCorrect ()
    {
        // Reset CM so properties are at hard-coded defaults
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
}
