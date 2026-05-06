using Xunit;

namespace Clet.UnitTests;

public class CommandLineRootTests
{
    private static (CommandLineRoot root, StringWriter stdout, StringWriter stderr) Build ()
    {
        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        return (new (registry), new (), new ());
    }

    [Fact]
    public async Task NoArgs_PrintsRootHelpAndExitsOk ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync ([], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("clet", stdout.ToString ());
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public async Task Help_PrintsRootHelp ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["--help"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("clet", stdout.ToString ());
    }

    [Fact]
    public async Task Version_PrintsCletAndTerminalGuiVersions ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["--version"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        string output = stdout.ToString ();
        Assert.Matches (@"^\d+\.\d+\.\d+(-\S+)? \(Terminal\.Gui \S+\)\s*$", output);
    }

    [Fact]
    public async Task HelpAlias_KnownAlias_PrintsAliasHelp ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["help", "select"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("select", stdout.ToString ());
        Assert.Contains ("--options", stdout.ToString ());
    }

    [Fact]
    public async Task HelpAlias_UnknownAlias_WritesStderrAndExits2 ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["help", "nope"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("unknown alias", stderr.ToString ());
    }

    [Fact]
    public async Task List_DefaultText_ListsRegisteredClets ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["list"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("select", stdout.ToString ());
    }

    [Fact]
    public async Task List_Json_EmitsSchemaVersion1 ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["list", "--json"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        string output = stdout.ToString ().TrimEnd ();
        Assert.Contains ("\"schemaVersion\":1", output);
        Assert.Contains ("\"alias\":\"select\"", output);
        Assert.Contains ("\"kind\":\"input\"", output);
        Assert.Contains ("\"resultType\":\"string\"", output);
    }

    [Theory]
    [InlineData ("100ms", 100)]
    [InlineData ("1s", 1000)]
    [InlineData ("2m", 120_000)]
    public void TryParseTimeout_ValidInput_Parses (string input, int expectedMs)
    {
        Assert.True (CommandLineRoot.TryParseTimeout (input, out TimeSpan result));
        Assert.Equal (expectedMs, (int)result.TotalMilliseconds);
    }

    [Theory]
    [InlineData ("")]
    [InlineData ("abc")]
    [InlineData ("0s")]
    [InlineData ("-5s")]
    [InlineData ("5x")]
    public void TryParseTimeout_InvalidInput_ReturnsFalse (string input)
    {
        Assert.False (CommandLineRoot.TryParseTimeout (input, out _));
    }

    [Fact]
    public async Task UnknownAlias_WritesStderrAndExits2 ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["nope"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("unknown alias", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_TimeoutMissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--timeout"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--timeout", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_TimeoutInvalidValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--timeout", "wat"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task Alias_TitleMissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--title"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--title", stderr.ToString ());
    }

    [Fact]
    public async Task RootHelp_MentionsTitleFlag ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["--help"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("--title", stdout.ToString ());
    }

    [Fact]
    public async Task List_ShortJsonFlag_EmitsJson ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["list", "-j"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("\"schemaVersion\":1", stdout.ToString ());
    }

    [Fact]
    public async Task Alias_ShortTitleFlag_MissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "-t"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--title", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_ShortInitialFlag_MissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "-i"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--initial", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_PositionalArgs_NonPositionalClet_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["int", "42"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("does not accept positional arguments", stderr.ToString ());
        Assert.Contains ("42", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_PositionalArgs_NonPositionalClet_SingleArg_ShowsInitialHint ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["int", "42"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        string stderrStr = stderr.ToString ();
        Assert.Contains ("hint:", stderrStr);
        Assert.Contains ("--initial", stderrStr);
        Assert.Contains ("42", stderrStr);
    }

    [Fact]
    public async Task Alias_PositionalArgs_NonPositionalClet_MultipleArgs_NoHint ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["int", "foo", "bar"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        string stderrStr = stderr.ToString ();
        Assert.Contains ("does not accept positional arguments", stderrStr);
        Assert.DoesNotContain ("hint:", stderrStr);
    }

    [Fact]
    public async Task Alias_PositionalArgs_NonPositionalClet_SingleDash_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        // bare "-" is treated as a positional arg (not a flag), and should be rejected
        int exit = await root.InvokeAsync (["int", "-"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("does not accept positional arguments", stderr.ToString ());
    }
}
