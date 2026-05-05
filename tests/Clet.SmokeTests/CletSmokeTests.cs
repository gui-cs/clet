using Xunit;

namespace Clet.SmokeTests;

// Smoke matrix: process-level invocation of the clet binary (Process.Start).
// Tests assert on exit code + stdout/stderr. They do not synthesize keystrokes.
//
// The keystroke-driven cancel case (SelectTimeout_EmitsCancelEnvelopeAndExits130)
// is still skipped. TUIcast wiring was planned at v0.3 but deferred — see
// decisions log D-007 and bar-raise backlog #BR-7.
public class CletSmokeTests
{
    [Fact]
    public async Task Version_PrintsVersionAndExitsZero ()
    {
        (int exit, string stdout, string stderr) = await CletProcess.RunAsync (["--version"]);

        Assert.Equal (0, exit);
        Assert.Empty (stderr);
        Assert.Matches (@"^\d+\.\d+\.\d+\s*$", stdout);
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

    [Fact (Skip = "Requires PTY + keystroke injection; deferred — see decisions log D-007 and bar-raise #BR-7.")]
    public Task SelectTimeout_EmitsCancelEnvelopeAndExits130 ()
    {
        // The cancellation contract is unit-tested in ExitCodesTests + OutputFormatterTests.
        // A process-level test needs a PTY harness (TUIcast or equivalent) to drive the binary.
        return Task.CompletedTask;
    }
}
