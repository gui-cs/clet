using System.Runtime.CompilerServices;

namespace Clet.IntegrationTests;

/// <summary>
/// Disables real driver I/O when running without an interactive console (e.g. CI).
/// Without this, the ANSI driver hangs on Windows CI trying to set up console handles.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init ()
    {
        if (!Environment.UserInteractive || Console.IsInputRedirected)
        {
            Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        }
    }
}
