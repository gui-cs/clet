using Xunit;

namespace Clet.UnitTests;

public class DurationCletTests
{
    [Fact]
    public void PrimaryAlias_IsDuration ()
    {
        DurationClet clet = new ();

        Assert.Equal ("duration", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        DurationClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsString ()
    {
        DurationClet clet = new ();

        Assert.Equal (typeof (string), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        DurationClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsDuration ()
    {
        DurationClet clet = new ();

        Assert.Contains ("duration", clet.Aliases);
    }

    [Fact]
    public void Options_IsEmpty ()
    {
        DurationClet clet = new ();

        Assert.Empty (clet.Options);
    }
}
