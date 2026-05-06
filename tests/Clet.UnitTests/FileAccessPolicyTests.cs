using Xunit;

namespace Clet.UnitTests;

public class FileAccessPolicyTests
{
    [Fact]
    public void AllowedExtension_InWorkingDirectory_Passes ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "readme.md");
        File.WriteAllText (file, "# hello");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void DisallowedExtension_IsRefused ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "secrets.conf");
        File.WriteAllText (file, "password=secret");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.NotNull (error);
            Assert.Contains ("not in the allowlist", error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void FileOutsideCwd_IsRefused ()
    {
        string cwd = Path.Combine (Path.GetTempPath (), "clet-test-cwd");
        Directory.CreateDirectory (cwd);
        string outside = Path.Combine (Path.GetTempPath (), "outside.md");
        File.WriteAllText (outside, "# outside");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (outside);

            Assert.NotNull (error);
            Assert.Contains ("outside the working directory", error);
        }
        finally
        {
            File.Delete (outside);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void AllowFile_BypassesExtensionAndCwdChecks ()
    {
        string cwd = Path.Combine (Path.GetTempPath (), "clet-test-cwd2");
        Directory.CreateDirectory (cwd);
        string outside = Path.Combine (Path.GetTempPath (), "credentials.conf");
        File.WriteAllText (outside, "key=value");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: [outside], allowBinary: false);
            string? error = policy.CheckFile (outside);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (outside);
            Directory.Delete (cwd, recursive: true);
        }
    }

    [Fact]
    public void BinaryFile_IsRefused_WhenAllowBinaryFalse ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "binary.md");
        byte[] content = [0x23, 0x20, 0x48, 0x65, 0x6C, 0x00, 0x6C, 0x6F]; // "# Hel\0lo"
        File.WriteAllBytes (file, content);

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.NotNull (error);
            Assert.Contains ("binary file", error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void BinaryFile_IsAllowed_WhenAllowBinaryTrue ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "binary.md");
        byte[] content = [0x23, 0x20, 0x48, 0x65, 0x6C, 0x00, 0x6C, 0x6F]; // "# Hel\0lo"
        File.WriteAllBytes (file, content);

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: true);
            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void GlobAggregate_TooManyFiles_IsRefused ()
    {
        // Create a list of file paths exceeding MaxGlobFiles
        List<string> files = [];

        for (int i = 0; i <= FileAccessPolicy.MaxGlobFiles; i++)
        {
            files.Add ($"/tmp/file{i}.md");
        }

        FileAccessPolicy policy = new (Path.GetTempPath (), allowedFiles: null, allowBinary: false);
        string? error = policy.CheckGlobAggregate (files);

        Assert.NotNull (error);
        Assert.Contains ("exceeding the maximum", error);
    }

    [Fact]
    public void OversizedFile_IsRefused ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "large.md");

        // Create a file slightly larger than 16 MiB
        using (FileStream fs = File.Create (file))
        {
            fs.SetLength (FileAccessPolicy.MaxFileSizeBytes + 1);
        }

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.NotNull (error);
            Assert.Contains ("per-file limit", error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void TxtExtension_IsAllowed ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "notes.txt");
        File.WriteAllText (file, "some notes");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
        }
    }

    [Fact]
    public void MarkdownExtension_IsAllowed ()
    {
        string cwd = Path.GetTempPath ();
        string file = Path.Combine (cwd, "readme.markdown");
        File.WriteAllText (file, "# hello");

        try
        {
            FileAccessPolicy policy = new (cwd, allowedFiles: null, allowBinary: false);
            string? error = policy.CheckFile (file);

            Assert.Null (error);
        }
        finally
        {
            File.Delete (file);
        }
    }
}
