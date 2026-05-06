using Xunit;

namespace Clet.UnitTests;

public class MultilineTextCletTests
{
    [Fact]
    public void PrimaryAlias_IsMultilineText ()
    {
        MultilineTextClet clet = new ();

        Assert.Equal ("multiline-text", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        MultilineTextClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        MultilineTextClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        MultilineTextClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsMultilineText ()
    {
        MultilineTextClet clet = new ();

        Assert.Contains ("multiline-text", clet.Aliases);
    }

    [Fact]
    public void Aliases_ContainsMt ()
    {
        MultilineTextClet clet = new ();

        Assert.Contains ("mt", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        MultilineTextClet clet = new ();

        Assert.Empty (clet.Options);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        IClet clet = new MultilineTextClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
