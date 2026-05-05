using Xunit;

namespace Clet.UnitTests;

public class IntCletTests
{
    [Fact]
    public void PrimaryAlias_IsInt ()
    {
        IntClet clet = new ();

        Assert.Equal ("int", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        IntClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsInt ()
    {
        IntClet clet = new ();

        Assert.Equal (typeof (int), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        IntClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsInt ()
    {
        IntClet clet = new ();

        Assert.Contains ("int", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsMinMaxStep ()
    {
        IntClet clet = new ();

        Assert.Equal (3, clet.Options.Count);
        Assert.Equal ("min", clet.Options [0].Name);
        Assert.Equal ("max", clet.Options [1].Name);
        Assert.Equal ("step", clet.Options [2].Name);
        Assert.False (clet.Options [0].Required);
        Assert.False (clet.Options [1].Required);
        Assert.False (clet.Options [2].Required);
    }
}
