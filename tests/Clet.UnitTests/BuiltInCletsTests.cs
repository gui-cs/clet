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
}
