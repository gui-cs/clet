using Xunit;

namespace Clet.UnitTests;

public class CletRegistryTests
{
    [Fact]
    public void Register_AddsToAll ()
    {
        CletRegistry registry = new ();
        SelectClet clet = new ();

        registry.Register (clet);

        Assert.Single (registry.All);
        Assert.Same (clet, registry.All.First ());
    }

    [Fact]
    public void TryResolve_FindsByAlias ()
    {
        CletRegistry registry = new ();
        SelectClet clet = new ();
        registry.Register (clet);

        bool found = registry.TryResolve ("select", out IClet? resolved);

        Assert.True (found);
        Assert.Same (clet, resolved);
    }

    [Fact]
    public void TryResolve_CaseInsensitive ()
    {
        CletRegistry registry = new ();
        registry.Register (new SelectClet ());

        bool found = registry.TryResolve ("SELECT", out IClet? resolved);

        Assert.True (found);
        Assert.NotNull (resolved);
    }

    [Fact]
    public void TryResolve_ReturnsFalse_ForUnknownAlias ()
    {
        CletRegistry registry = new ();

        bool found = registry.TryResolve ("unknown", out IClet? resolved);

        Assert.False (found);
        Assert.Null (resolved);
    }

    [Fact]
    public void Register_ThrowsOnDuplicateAlias ()
    {
        CletRegistry registry = new ();
        registry.Register (new SelectClet ());

        Assert.Throws<InvalidOperationException> (() => registry.Register (new SelectClet ()));
    }

    [Fact]
    public void All_IsEmpty_Initially ()
    {
        CletRegistry registry = new ();

        Assert.Empty (registry.All);
    }
}
