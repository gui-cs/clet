using Xunit;

namespace Clet.UnitTests;

public class MarkdownContentResolverTests
{
    [Fact]
    public void Resolve_InlineContent_ReturnsContent ()
    {
        CletRunOptions options = new ();

        var result = MarkdownContentResolver.Resolve ("# Hello", options, stdinReader: null);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# Hello", result.Content);
        Assert.Empty (result.Files);
    }

    [Fact]
    public void Resolve_InlineContent_TakesPriorityOverStdin ()
    {
        CletRunOptions options = new ();
        using StringReader stdin = new ("stdin content");

        var result = MarkdownContentResolver.Resolve ("# Inline", options, stdin);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# Inline", result.Content);
    }

    [Fact]
    public void Resolve_Stdin_ReturnsContent ()
    {
        CletRunOptions options = new ();
        using StringReader stdin = new ("# From Stdin");

        var result = MarkdownContentResolver.Resolve (null, options, stdin);

        Assert.True (result.IsSuccess);
        Assert.Equal ("# From Stdin", result.Content);
        Assert.Empty (result.Files);
    }

    [Fact]
    public void Resolve_EmptyStdin_ReturnsError ()
    {
        CletRunOptions options = new ();
        using StringReader stdin = new ("");

        var result = MarkdownContentResolver.Resolve (null, options, stdin);

        Assert.False (result.IsSuccess);
        Assert.Equal ("io", result.ErrorCode);
    }

    [Fact]
    public void Resolve_NoSource_ReturnsError ()
    {
        CletRunOptions options = new ();

        var result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

        Assert.False (result.IsSuccess);
        Assert.Equal ("io", result.ErrorCode);
        Assert.Contains ("No file specified", result.ErrorMessage!);
    }

    [Fact]
    public void Resolve_FileArgs_ReadsExistingFiles ()
    {
        // Create a temp markdown file
        string tempDir = Path.Combine (Path.GetTempPath (), "clet-test-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (tempDir);

        try
        {
            string file = Path.Combine (tempDir, "test.md");
            File.WriteAllText (file, "# Test File");

            // Need to set CWD to tempDir so the file access policy allows access
            string originalDir = Directory.GetCurrentDirectory ();
            Directory.SetCurrentDirectory (tempDir);

            try
            {
                CletRunOptions options = new () { Arguments = [file] };

                var result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

                Assert.True (result.IsSuccess);
                Assert.Equal ("# Test File", result.Content);
                Assert.Single (result.Files);
            }
            finally
            {
                Directory.SetCurrentDirectory (originalDir);
            }
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void Resolve_FileArgs_NonexistentFile_ReturnsError ()
    {
        CletRunOptions options = new () { Arguments = ["/nonexistent/file.md"] };

        var result = MarkdownContentResolver.Resolve (null, options, stdinReader: null);

        Assert.False (result.IsSuccess);
    }

    [Fact]
    public void Resolve_FileArgs_TakesPriorityOverInline ()
    {
        string tempDir = Path.Combine (Path.GetTempPath (), "clet-test-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (tempDir);

        try
        {
            string file = Path.Combine (tempDir, "priority.md");
            File.WriteAllText (file, "# From File");

            string originalDir = Directory.GetCurrentDirectory ();
            Directory.SetCurrentDirectory (tempDir);

            try
            {
                CletRunOptions options = new () { Arguments = [file] };

                var result = MarkdownContentResolver.Resolve ("# Inline", options, stdinReader: null);

                Assert.True (result.IsSuccess);
                Assert.Equal ("# From File", result.Content);
            }
            finally
            {
                Directory.SetCurrentDirectory (originalDir);
            }
        }
        finally
        {
            Directory.Delete (tempDir, true);
        }
    }

    [Fact]
    public void ExpandFiles_NonexistentFile_ReturnsEmptyWithWarning ()
    {
        FileAccessPolicy policy = new (
            Directory.GetCurrentDirectory (),
            allowedFiles: null,
            allowBinary: false);

        List<string> files = MarkdownContentResolver.ExpandFiles (
            ["/nonexistent/file.md"],
            policy,
            out string? error);

        // File doesn't exist, so it's skipped (warning printed) and result is empty
        Assert.Empty (files);
        Assert.Null (error);
    }
}
