using Xunit;

namespace Clet.UnitTests;

public class MarkdownCletTests
{
    [Fact]
    public void PrimaryAlias_IsMd ()
    {
        MarkdownClet clet = new ();

        Assert.Equal ("md", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsViewer ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (CletKind.Viewer, clet.Kind);
    }

    [Fact]
    public void ResultType_IsVoid ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (typeof (void), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        MarkdownClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsMd ()
    {
        MarkdownClet clet = new ();

        Assert.Contains ("md", clet.Aliases);
    }

    [Fact]
    public void Aliases_ContainsMarkdown ()
    {
        MarkdownClet clet = new ();

        Assert.Contains ("markdown", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsThemeCatAndAllowExternalLinks ()
    {
        MarkdownClet clet = new ();

        Assert.Equal (3, clet.Options.Count);
        Assert.Equal ("theme", clet.Options [0].Name);
        Assert.Equal ("cat", clet.Options [1].Name);
        Assert.Equal ("allow-external-links", clet.Options [2].Name);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        MarkdownClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_RelativeMdFile_Resolves ()
    {
        // Create a temp .md file within a sandbox
        string sandbox = Path.GetFullPath (Path.GetTempPath ());
        string tempFile = Path.Combine (sandbox, $"test-{Guid.NewGuid ()}.md");
        File.WriteAllText (tempFile, "# Test");

        try
        {
            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                Path.GetFileName (tempFile), sandbox, sandbox, allowExternal: false, out string? resolved);

            Assert.True (result);
            Assert.Equal (tempFile, resolved);
        }
        finally
        {
            File.Delete (tempFile);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_OutsideSandbox_Blocked ()
    {
        string sandbox = Path.GetFullPath (Path.Combine (Path.GetTempPath (), "sandbox-test"));
        string outsideFile = Path.GetFullPath (Path.Combine (Path.GetTempPath (), "outside.md"));
        Directory.CreateDirectory (sandbox);
        File.WriteAllText (outsideFile, "# Outside");

        try
        {
            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                outsideFile, sandbox, sandbox, allowExternal: false, out _);

            Assert.False (result);
        }
        finally
        {
            File.Delete (outsideFile);
            Directory.Delete (sandbox);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_OutsideSandbox_AllowedWithFlag ()
    {
        string sandbox = Path.GetFullPath (Path.Combine (Path.GetTempPath (), "sandbox-test2"));
        string outsideFile = Path.GetFullPath (Path.Combine (Path.GetTempPath (), "outside2.md"));
        Directory.CreateDirectory (sandbox);
        File.WriteAllText (outsideFile, "# Outside");

        try
        {
            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                outsideFile, sandbox, sandbox, allowExternal: true, out string? resolved);

            Assert.True (result);
            Assert.Equal (outsideFile, resolved);
        }
        finally
        {
            File.Delete (outsideFile);
            Directory.Delete (sandbox);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_HttpUrl_Rejected ()
    {
        bool result = MarkdownClet.TryResolveLocalMarkdownLink (
            "https://example.com/README.md", "/tmp", "/tmp", allowExternal: false, out _);

        Assert.False (result);
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_NonMdFile_Rejected ()
    {
        string sandbox = Path.GetFullPath (Path.GetTempPath ());
        string tempFile = Path.Combine (sandbox, $"test-{Guid.NewGuid ()}.txt");
        File.WriteAllText (tempFile, "not markdown");

        try
        {
            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                Path.GetFileName (tempFile), sandbox, sandbox, allowExternal: false, out _);

            Assert.False (result);
        }
        finally
        {
            File.Delete (tempFile);
        }
    }

    [Fact]
    public void TryResolveLocalMarkdownLink_FragmentStripped ()
    {
        string sandbox = Path.GetFullPath (Path.GetTempPath ());
        string tempFile = Path.Combine (sandbox, $"test-{Guid.NewGuid ()}.md");
        File.WriteAllText (tempFile, "# Test");

        try
        {
            bool result = MarkdownClet.TryResolveLocalMarkdownLink (
                Path.GetFileName (tempFile) + "#section", sandbox, sandbox, allowExternal: false, out string? resolved);

            Assert.True (result);
            Assert.Equal (tempFile, resolved);
        }
        finally
        {
            File.Delete (tempFile);
        }
    }
}
