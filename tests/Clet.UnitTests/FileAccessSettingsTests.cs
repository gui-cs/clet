using System.Text.Json;
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
    public void MergeWithConfigPaths_BothNull_ReturnsNull ()
    {
        FileAccessSettings.AllowedPaths = null;

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

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (null);

        Assert.NotNull (result);
        Assert.Single (result);
        Assert.Equal ("/tmp/config-dir", result[0]);

        FileAccessSettings.AllowedPaths = null;
    }

    [Fact]
    public void MergeWithConfigPaths_BothProvided_CombinesCliThenConfig ()
    {
        FileAccessSettings.AllowedPaths = ["/config/path"];
        List<string> cli = ["/cli/path"];

        IReadOnlyList<string>? result = FileAccessPolicy.MergeWithConfigPaths (cli);

        Assert.NotNull (result);
        Assert.Equal (2, result.Count);
        Assert.Equal ("/cli/path", result[0]);
        Assert.Equal ("/config/path", result[1]);

        FileAccessSettings.AllowedPaths = null;
    }

    // ── LoadFromConfig ───────────────────────────────────────────────────────

    [Fact]
    public void LoadFromConfig_FileNotFound_LeavesAllowedPathsNull ()
    {
        FileAccessSettings.AllowedPaths = ["/existing"];

        FileAccessSettings.LoadFromConfig ("/nonexistent/path/clet.config.json");

        Assert.Null (FileAccessSettings.AllowedPaths);
    }

    [Fact]
    public void LoadFromConfig_ValidArray_SetsAllowedPaths ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, """
                {
                  "FileAccessSettings.AllowedPaths": ["/home/user/projects", "/tmp/docs"]
                }
                """);

            FileAccessSettings.AllowedPaths = null;
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.NotNull (FileAccessSettings.AllowedPaths);
            Assert.Equal (2, FileAccessSettings.AllowedPaths.Length);
            Assert.Equal ("/home/user/projects", FileAccessSettings.AllowedPaths[0]);
            Assert.Equal ("/tmp/docs", FileAccessSettings.AllowedPaths[1]);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
        }
    }

    [Fact]
    public void LoadFromConfig_ArrayWithComments_SetsAllowedPaths ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, """
                {
                  // Allow list for clet edit and clet md
                  "FileAccessSettings.AllowedPaths": [
                    // My projects
                    "/home/user/projects",
                    "/tmp/docs"
                  ]
                }
                """);

            FileAccessSettings.AllowedPaths = null;
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.NotNull (FileAccessSettings.AllowedPaths);
            Assert.Equal (2, FileAccessSettings.AllowedPaths.Length);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
        }
    }

    [Fact]
    public void LoadFromConfig_EmptyArray_LeavesAllowedPathsNull ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, """
                {
                  "FileAccessSettings.AllowedPaths": []
                }
                """);

            FileAccessSettings.AllowedPaths = ["/existing"];
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.Null (FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
        }
    }

    [Fact]
    public void LoadFromConfig_KeyAbsent_LeavesAllowedPathsNull ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, """
                {
                  "EditorSettings.LineNumbers": true
                }
                """);

            FileAccessSettings.AllowedPaths = ["/existing"];
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.Null (FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
        }
    }

    [Fact]
    public void LoadFromConfig_InvalidJson_LeavesAllowedPathsNull ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, "not valid json {{{");

            FileAccessSettings.AllowedPaths = ["/existing"];
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.Null (FileAccessSettings.AllowedPaths);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
        }
    }

    [Fact]
    public void LoadFromConfig_IgnoresBlankAndWhitespaceEntries ()
    {
        string configPath = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ():N}.json");

        try
        {
            File.WriteAllText (configPath, """
                {
                  "FileAccessSettings.AllowedPaths": ["", "  ", "/valid/path", null]
                }
                """);

            FileAccessSettings.AllowedPaths = null;
            FileAccessSettings.LoadFromConfig (configPath);

            Assert.NotNull (FileAccessSettings.AllowedPaths);
            Assert.Single (FileAccessSettings.AllowedPaths);
            Assert.Equal ("/valid/path", FileAccessSettings.AllowedPaths[0]);
        }
        finally
        {
            FileAccessSettings.AllowedPaths = null;
            File.Delete (configPath);
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
            FileAccessSettings.AllowedPaths = null;
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
            FileAccessSettings.AllowedPaths = null;
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
            FileAccessSettings.AllowedPaths = null;
            File.Delete (file);
            Directory.Delete (allowedDir, recursive: true);
            Directory.Delete (cwd, recursive: true);
            Directory.Delete (otherDir, recursive: true);
        }
    }
}
