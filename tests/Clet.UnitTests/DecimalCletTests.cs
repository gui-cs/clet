using Xunit;

namespace Clet.UnitTests;

public class DecimalCletTests
{
    [Fact]
    public void PrimaryAlias_IsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Equal ("decimal", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        DecimalClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Equal (typeof (decimal), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        DecimalClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsDecimal ()
    {
        DecimalClet clet = new ();

        Assert.Contains ("decimal", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsMinMaxStep ()
    {
        DecimalClet clet = new ();

        Assert.Equal (3, clet.Options.Count);
        Assert.Equal ("min", clet.Options [0].Name);
        Assert.Equal ("max", clet.Options [1].Name);
        Assert.Equal ("step", clet.Options [2].Name);
    }
}
