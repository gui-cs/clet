using System.Runtime.CompilerServices;

namespace Clet.UnitTests;

/// <summary>
/// Disables real driver I/O so unit tests never interact with the terminal or launch processes.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init ()
    {
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
    }
}
