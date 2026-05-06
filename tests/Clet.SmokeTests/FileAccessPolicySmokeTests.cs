using Xunit;

namespace Clet.SmokeTests;

/// <summary>
/// Smoke tests for file-access confinement in <c>clet md</c>.
/// Verifies that the security policy rejects unauthorized file access.
/// </summary>
public class FileAccessPolicySmokeTests
{
    [Fact]
    public async Task MdSystemFile_IsRefusedWithoutAllowFile ()
    {
        // /etc/passwd on Linux, or a well-known non-.md system file
        string systemFile = OperatingSystem.IsWindows ()
            ? @"C:\Windows\System32\drivers\etc\hosts"
            : "/etc/passwd";

        if (!File.Exists (systemFile))
        {
            return; // Skip if the file doesn't exist on this system
        }

        (int exit, _, string stderr) = await CletProcess.RunAsync (
            ["md", "--cat", systemFile]);

        // Should be refused — not in allowlist (.md/.markdown/.txt) or outside cwd
        Assert.NotEqual (0, exit);
        Assert.Contains ("Refused", stderr);
    }

    [Fact]
    public async Task MdSystemFile_AllowedWithAllowFile ()
    {
        // Create a .conf file in /tmp to test --allow-file bypass
        string tmpFile = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ()}.conf");
        File.WriteAllText (tmpFile, "# test config content");

        try
        {
            (int exit, _, string stderr) = await CletProcess.RunAsync (
                ["md", "--cat", "--allow-file", tmpFile, tmpFile]);

            Assert.Equal (0, exit);
            Assert.Empty (stderr);
        }
        finally
        {
            File.Delete (tmpFile);
        }
    }

    [Fact]
    public async Task MdGlobOutsideCwd_IsRefused ()
    {
        // Try to glob /etc/*.conf (Linux) — should be refused
        if (OperatingSystem.IsWindows ())
        {
            return; // Skip on Windows — different path structure
        }

        (int exit, _, string stderr) = await CletProcess.RunAsync (
            ["md", "--cat", "/etc/*.conf"]);

        // Should be refused due to being outside cwd + disallowed extension
        Assert.NotEqual (0, exit);
        Assert.Contains ("Refused", stderr);
    }

    [Fact]
    public async Task MdAllowedExtension_InCwd_Succeeds ()
    {
        // Create a .md file and run from its directory
        string tmpDir = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ()}");
        Directory.CreateDirectory (tmpDir);
        string mdFile = Path.Combine (tmpDir, "test.md");
        File.WriteAllText (mdFile, "# Hello World");

        try
        {
            (int exit, string stdout, string stderr) = await CletProcess.RunAsync (
                ["md", "--cat", mdFile, "--allow-file", mdFile]);

            // With --allow-file, should succeed even though it may be outside the process cwd
            Assert.Equal (0, exit);
        }
        finally
        {
            Directory.Delete (tmpDir, recursive: true);
        }
    }

    [Fact]
    public async Task MdBinaryFile_IsRefused ()
    {
        string tmpFile = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ()}.md");
        byte[] content = [0x23, 0x20, 0x48, 0x65, 0x6C, 0x00, 0x6C, 0x6F]; // "# Hel\0lo"
        File.WriteAllBytes (tmpFile, content);

        try
        {
            (int exit, string stdout, string stderr) = await CletProcess.RunAsync (
                ["md", "--cat", "--allow-file", tmpFile, tmpFile]);

            Assert.NotEqual (0, exit);
            Assert.Contains ("binary file", stderr);
        }
        finally
        {
            File.Delete (tmpFile);
        }
    }

    [Fact]
    public async Task MdBinaryFile_AllowedWithAllowBinary ()
    {
        string tmpFile = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ()}.md");
        byte[] content = [0x23, 0x20, 0x48, 0x65, 0x6C, 0x00, 0x6C, 0x6F]; // "# Hel\0lo"
        File.WriteAllBytes (tmpFile, content);

        try
        {
            (int exit, string stdout, string stderr) = await CletProcess.RunAsync (
                ["md", "--cat", "--allow-file", tmpFile, "--allow-binary", tmpFile]);

            Assert.Equal (0, exit);
        }
        finally
        {
            File.Delete (tmpFile);
        }
    }
}
