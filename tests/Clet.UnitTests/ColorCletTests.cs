using Xunit;

namespace Clet.UnitTests;

public class ColorCletTests
{
    [Fact]
    public void PrimaryAlias_IsColor ()
    {
        ColorClet clet = new ();

        Assert.Equal ("color", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        ColorClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        ColorClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        ColorClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsColor ()
    {
        ColorClet clet = new ();

        Assert.Contains ("color", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        ColorClet clet = new ();

        Assert.Empty (clet.Options);
    }
}
