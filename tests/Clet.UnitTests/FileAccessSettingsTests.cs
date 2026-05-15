using Terminal.Gui.Configuration;
using Xunit;

namespace Clet.UnitTests;

/// <summary>
/// Tests for <see cref="FileAccessSettings"/> and
/// <see cref="FileAccessPolicy.MergeWithConfigPaths"/>.
/// </summary>
public class FileAccessSettingsTests
{
    // ── MergeWithConfigPaths ─────────────────────────────────────────────────

    [Fact]
    public void MergeWithConfigPaths_ConfigEmptyAndCliNull_ReturnsNull ()
    {
        FileAccessSettings.AllowedPaths = [];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (null);

        Assert.Null (result);
    }

    [Fact]
    public void MergeWithConfigPaths_EmptyConfig_ReturnsCli ()
    {
        FileAccessSettings.AllowedPaths = [];
        List<string> cli = ["/tmp/a"];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (cli);

        Assert.Same (cli, result);
    }

    [Fact]
    public void MergeWithConfigPaths_NullCli_ReturnsConfigPaths ()
    {
        FileAccessSettings.AllowedPaths = ["/tmp/config-dir"];

        try
        {
            IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (null);

            Assert.NotNull (result);
            Assert.Single (result);
            Assert.Equal ("/tmp/config-dir", result[0]);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = [];
        }
    }

    [Fact]
    public void MergeWithConfigPaths_BothProvided_CombinesCliThenConfig ()
    {
        FileAccessSettings.AllowedPaths = ["/config/path"];
        List<string> cli = ["/cli/path"];

        try
        {
            IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (cli);

            Assert.NotNull (result);
            Assert.Equal (2, result.Count);
            Assert.Equal ("/cli/path", result[0]);
            Assert.Equal ("/config/path", result[1]);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = [];
        }
    }

    // ── FileAccessPolicy integration with config paths ───────────────────────

    [Fact]
    public void Policy_ConfigPath_AllowsFileOutsideCwdWithoutCliFlag ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-allow-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);

        string file = Path.Combine (allowedDir, "notes.txt");
        File.WriteAllText (file, "hello");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = [];
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void Policy_ConfigPath_AllowsDisallowedExtension ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-ext-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd-ext-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);

        string file = Path.Combine (allowedDir, "config.yaml");
        File.WriteAllText (file, "key: value");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = [];
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void Policy_ConfigPath_FileNotInAllowedDir_IsStillRefused ()
    {
        string allowedDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-allow2-{Guid.NewGuid ():N}");
        string cwd = Path.Combine (Path.GetTempPath (), $"clet-cfg-cwd2-{Guid.NewGuid ():N}");
        string otherDir = Path.Combine (Path.GetTempPath (), $"clet-cfg-other-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (allowedDir);
        Directory.CreateDirectory (cwd);
        Directory.CreateDirectory (otherDir);

        string file = Path.Combine (otherDir, "notes.conf");
        File.WriteAllText (file, "key=val");

        try
        {
            FileAccessSettings.AllowedPaths = [allowedDir];

            FileAccessPolicy policy = new (
                cwd,
                FileAccessPolicy.MergeWithConfigPaths (null),
                allowBinary: false);

            string? error = policy.CheckFile (file);

            Assert.NotNull (error);
            Assert.Contains ("Refused", error);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = [];
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
            Directory.Delete (otherDir, recursive: true);
        }
    }
}

/// <summary>
/// Tests that verify <see cref="FileAccessSettings.AllowedPaths"/> is discovered
/// by <see cref="ConfigurationManager"/> and loaded via JSON with the
/// <see cref="StringArrayJsonConverter"/>.
/// Must not run in parallel with other CM tests.
/// </summary>
[Collection (nameof (ConfigurationManagerCollection))]
public class FileAccessSettingsCmTests : IDisposable
{
    private readonly string? _originalHome;
    private readonly string _tempDir;

    public FileAccessSettingsCmTests ()
    {
        _tempDir = Path.Combine (Path.GetTempPath (), $"clet-fas-test-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (_tempDir);
        _originalHome = Environment.GetEnvironmentVariable ("HOME");
        Environment.SetEnvironmentVariable ("HOME", _tempDir);
        ConfigurationManager.AppName = "clet";
        FileAccessSettings.AllowedPaths = [];
    }

    public void Dispose ()
    {
        FileAccessSettings.AllowedPaths = [];

        try
        {
            ConfigurationManager.Disable (resetToHardCodedDefaults: true);
        }
        catch
        {
            // Best-effort cleanup.
        }

        Environment.SetEnvironmentVariable ("HOME", _originalHome);

        if (Directory.Exists (_tempDir))
        {
            Directory.Delete (_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigurationManager_Discovers_FileAccessSettings_AllowedPaths ()
    {
        ConfigurationManager.Enable (ConfigLocations.None);

        Assert.True (
            ConfigurationManager.Settings!.Keys.Contains ("FileAccessSettings.AllowedPaths"),
            "ConfigurationManager.Settings should contain 'FileAccessSettings.AllowedPaths'");
    }

    [Fact]
    public void ConfigurationManager_Loads_AllowedPaths_FromRuntimeConfig ()
    {
        string json = """
            {
              "FileAccessSettings.AllowedPaths": ["/home/user/projects", "/tmp/docs"]
            }
            """;

        FileAccessSettings.AllowedPaths = [];

        ConfigurationManager.RuntimeConfig = json;
        ConfigurationManager.Enable (ConfigLocations.Runtime);

        Assert.NotNull (FileAccessSettings.AllowedPaths);
        Assert.Equal (2, FileAccessSettings.AllowedPaths.Length);
        Assert.Equal ("/home/user/projects", FileAccessSettings.AllowedPaths[0]);
        Assert.Equal ("/tmp/docs", FileAccessSettings.AllowedPaths[1]);
    }

    [Fact]
    public void ConfigurationManager_EmptyArray_SetsAllowedPathsToEmpty ()
    {
        string json = """
            {
              "FileAccessSettings.AllowedPaths": []
            }
            """;

        FileAccessSettings.AllowedPaths = ["/should-be-cleared"];

        ConfigurationManager.RuntimeConfig = json;
        ConfigurationManager.Enable (ConfigLocations.Runtime);

        Assert.Empty (FileAccessSettings.AllowedPaths);
    }
}
