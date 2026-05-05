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
    public void Options_IsEmpty ()
    {
        MarkdownClet clet = new ();

        Assert.Empty (clet.Options);
    }
}
