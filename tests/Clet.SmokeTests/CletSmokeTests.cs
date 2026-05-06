using Xunit;

namespace Clet.SmokeTests;

// v0.11 smoke matrix per issue #9: process-level invocation of the clet binary.
//
// Five of the six smoke cases land here. They use Process.Start and assert on
// exit code + stdout/stderr. They do not synthesize keystrokes.
//
// The sixth case (`clet select --json` happy-path with an Enter keystroke) is
// deferred to v0.3 when TUIcast is wired up — at v0.11 we have one input clet
// (select), and standing up a PTY/keystroke harness for a single happy-path
// case is not worth the dependency. v0.3 brings 13 more clets and TUIcast at
// the same time per spec §6.3.
public class CletSmokeTests
{
    [Fact]
    public async Task Version_PrintsVersionAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--version"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Matches (@"^\d+\.\d+\.\d+(-\S+)? \(Terminal\.Gui \S+\)\s*$", stdout);
    }

    [Fact]
    public async Task Help_PrintsUsageAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--help"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("clet", stdout);
        Assert.Contains ("select", stdout);
    }

    [Fact]
    public async Task ListJson_EmitsRegistryEnvelopeAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["list", "--json"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        string trimmed = stdout.TrimEnd ();
        Assert.Contains ("\"schemaVersion\":1", trimmed);
        Assert.Contains ("\"alias\":\"select\"", trimmed);
        Assert.Contains ("\"kind\":\"input\"", trimmed);
    }

    [Fact]
    public async Task HelpAlias_KnownAlias_PrintsAliasHelp ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["help", "select"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("select", stdout);
        Assert.Contains ("--options", stdout);
    }

    [Fact]
    public async Task HelpAlias_UnknownAlias_ExitsWithUsageError ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["help", "nope"]);

        Assert.Equal (2, exit);
        Assert.Contains ("unknown alias", stderr);
    }

    [Fact]
    public async Task ListJson_IncludesMdViewer ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["list", "--json"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("\"alias\":\"md\"", stdout);
        Assert.Contains ("\"kind\":\"viewer\"", stdout);
    }

    [Fact]
    public async Task HelpAlias_Md_PrintsMdHelp ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["help", "md"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Contains ("md", stdout);
        Assert.Contains ("Markdown", stdout);
    }

    [Fact (Skip = "Requires real TG run loop with cancellation; v0.3 TUIcast harness will drive this against the AOT'd binary.")]
    public Task SelectTimeout_EmitsCancelEnvelopeAndExits130 ()
    {
        // Intentionally not implemented at v0.11. The cancellation contract is unit-tested in
        // ExitCodesTests + OutputFormatterTests; a process-level test of the timeout path needs
        // the TUIcast harness from v0.3 to drive the binary in a controlled environment.
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OversizedInitial_ExitsWithValidationError ()
    {
        // Windows has a ~32K command-line length limit; the 64K --initial arg exceeds it.
        // The logic is covered by the unit test (Alias_InitialExceeds64KiB_ExitsWithValidationError).
        if (OperatingSystem.IsWindows ())
        {
            return;
        }

        string oversized = new ('x', 64 * 1024 + 1);

        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (
            ["select", "--json", "--initial", oversized]);

        Assert.Equal (65, exit);
        Assert.Contains ("input-too-large", stdout);
        Assert.Contains ("\"status\":\"error\"", stdout);
    }

    [Fact]
    public async Task MdOversizedStdin_ExitsWithValidationError ()
    {
        string oversized = new ('x', 8 * 1024 * 1024 + 1);

        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (
            ["md", "--json"], stdin: oversized);

        Assert.Equal (65, exit);
        Assert.Contains ("input-too-large", stdout);
        Assert.Contains ("\"status\":\"error\"", stdout);
    }
}
