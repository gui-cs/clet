using Xunit;

namespace Clet.UnitTests;

public class RangeCletTests
{
    [Fact]
    public void PrimaryAlias_IsRange ()
    {
        RangeClet clet = new ();

        Assert.Equal ("range", clet.PrimaryAlias);
    }

    [Fact]
    public void Kind_IsInput ()
    {
        RangeClet clet = new ();

        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Fact]
    public void ResultType_IsJsonObject ()
    {
        RangeClet clet = new ();

        Assert.Equal (typeof (System.Text.Json.Nodes.JsonObject), clet.ResultType);
    }

    [Fact]
    public void Description_IsNotEmpty ()
    {
        RangeClet clet = new ();

        Assert.NotEmpty (clet.Description);
    }

    [Fact]
    public void Aliases_ContainsRange ()
    {
        RangeClet clet = new ();

        Assert.Contains ("range", clet.Aliases);
    }

    [Fact]
    public void Options_ContainsStep ()
    {
        RangeClet clet = new ();

        Assert.Single (clet.Options);
        Assert.Equal ("step", clet.Options [0].Name);
        Assert.False (clet.Options [0].Required);
    }

    [Fact]
    public void AcceptsPositionalArgs_IsFalse ()
    {
        IClet clet = new RangeClet ();

        Assert.False (clet.AcceptsPositionalArgs);
    }
}
