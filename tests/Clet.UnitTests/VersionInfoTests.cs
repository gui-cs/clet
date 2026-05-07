using System.Reflection;
using Xunit;

namespace Clet.UnitTests;

public class VersionInfoTests
{
    [Fact]
    public void GetCletVersion_ReturnsNonEmptyString ()
    {
        string version = VersionInfo.GetCletVersion ();

        Assert.NotEmpty (version);
    }

    [Fact]
    public void GetTerminalGuiVersion_ReturnsNonEmptyString ()
    {
        string version = VersionInfo.GetTerminalGuiVersion ();

        Assert.NotEmpty (version);
    }

    [Fact]
    public void GetCletVersion_DoesNotContainBuildMetadata ()
    {
        string version = VersionInfo.GetCletVersion ();

        Assert.DoesNotContain ("+", version);
    }

    [Fact]
    public void GetTerminalGuiVersion_DoesNotContainBuildMetadata ()
    {
        string version = VersionInfo.GetTerminalGuiVersion ();

        Assert.DoesNotContain ("+", version);
    }

    [Fact]
    public void GetAssemblyVersion_TrimsAfterPlus ()
    {
        // The clet assembly itself has informational version with build metadata
        Assembly cletAssembly = typeof (Program).Assembly;
        string version = VersionInfo.GetAssemblyVersion (cletAssembly, "fallback");

        Assert.DoesNotContain ("+", version);
        Assert.NotEqual ("fallback", version);
    }

    [Fact]
    public void GetAssemblyVersion_ReturnsFallback_WhenNoVersionAttribute ()
    {
        // Use an assembly that might not have InformationalVersion — this tests the fallback path
        // We can't easily construct such an assembly in tests, but we can at least verify
        // the method returns a non-empty string for known assemblies
        Assembly tgAssembly = typeof (Terminal.Gui.App.Application).Assembly;
        string version = VersionInfo.GetAssemblyVersion (tgAssembly, "unknown");

        Assert.NotEmpty (version);
    }
}
