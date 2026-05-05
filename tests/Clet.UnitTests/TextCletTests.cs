using Xunit;

namespace Clet.UnitTests;

public class TextCletTests
{
    [Fact]
    public void PrimaryAlias_IsText ()
    {
        TextClet clet = new ();

        Assert.Equal ("text", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        TextClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        TextClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        TextClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsText ()
    {
        TextClet clet = new ();

        Assert.Contains ("text", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        TextClet clet = new ();

        Assert.Empty (clet.Options);
    }
}
