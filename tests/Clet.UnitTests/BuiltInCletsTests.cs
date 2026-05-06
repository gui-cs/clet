using Xunit;

namespace Clet.UnitTests;

public class BuiltInCletsTests
{
    [Fact]
    public void RegisterAll_RegistersSelect ()
    {
        ICletRegistry registry = new CletRegistry ();

        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve ("select", out IClet? clet));
        Assert.NotNull (clet);
        Assert.Equal ("select", clet!.PrimaryAlias);
        Assert.Equal (CletKind.Input, clet.Kind);
    }

    [Theory]
    [InlineData ("select")]
    [InlineData ("text")]
    [InlineData ("multiline-text")]
    [InlineData ("mt")]
    [InlineData ("int")]
    [InlineData ("decimal")]
    [InlineData ("confirm")]
    [InlineData ("date")]
    [InlineData ("time")]
    [InlineData ("duration")]
    [InlineData ("color")]
    [InlineData ("multi-select")]
    [InlineData ("attribute-picker")]
    [InlineData ("attribute")]
    [InlineData ("pick-file")]
    [InlineData ("file")]
    [InlineData ("pick-directory")]
    [InlineData ("dir")]
    [InlineData ("linear-range")]
    [InlineData ("range")]
    public void RegisterAll_RegistersInputClet (string alias)
    {
        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve (alias, out IClet? clet));
        Assert.NotNull (clet);
        Assert.Equal (CletKind.Input, clet!.Kind);
    }

    [Theory]
    [InlineData ("md")]
    [InlineData ("markdown")]
    [InlineData ("help")]
    public void RegisterAll_RegistersViewerClet (string alias)
    {
        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        Assert.True (registry.TryResolve (alias, out IClet? clet));
        Assert.NotNull (clet);
        Assert.Equal (CletKind.Viewer, clet!.Kind);
    }

    [Fact]
    public void RegisterAll_Registers17Clets ()
    {
        CletRegistry registry = new ();
        BuiltInClets.RegisterAll (registry);

        Assert.Equal (17, registry.All.Count);
    }
}
