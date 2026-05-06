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
        Assert.Contains ("clet", stdout.ToString (), StringComparison.OrdinalIgnoreCase);
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public async Task Help_PrintsRootHelp ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["--help"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.Contains ("clet", stdout.ToString (), StringComparison.OrdinalIgnoreCase);
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
    public async Task HelpAlias_KnownAlias_DispatchesHelpViewer ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();
        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        int exit = await root.InvokeAsync (["help", "select"], cts.Token, stdout, stderr);

        // Pre-cancelled token causes the md viewer to return Cancelled immediately,
        // proving that help <alias> dispatches through the interactive help viewer.
        Assert.Equal (ExitCodes.Cancelled, exit);
        Assert.Empty (stderr.ToString ());
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

    [Fact]
    public async Task Alias_RowsMissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--rows"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--rows", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_RowsInvalidValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--rows", "bad"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--rows", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_RowsZeroValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--rows", "0"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task Alias_ShortRowsFlag_MissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "-r"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--rows", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_InitialExceeds64KiB_ExitsWithValidationError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();
        string oversized = new ('x', CommandLineRoot.MaxInitialChars + 1);

        int exit = await root.InvokeAsync (["select", "--initial", oversized], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.ValidationError, exit);
        Assert.Contains ("input-too-large", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_InitialExceeds64KiB_Json_EmitsErrorEnvelope ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();
        string oversized = new ('x', CommandLineRoot.MaxInitialChars + 1);

        int exit = await root.InvokeAsync (["select", "--json", "--initial", oversized], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.ValidationError, exit);
        string json = stdout.ToString ().TrimEnd ();
        Assert.Contains ("\"status\":\"error\"", json);
        Assert.Contains ("\"code\":\"input-too-large\"", json);
    }

    [Fact]
    public async Task Alias_InitialAtExactLimit_DoesNotReject ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();
        string atLimit = new ('x', CommandLineRoot.MaxInitialChars);

        // Use a pre-cancelled token so dispatch exits immediately without starting TUI.
        // We're verifying the size cap doesn't trip at the boundary; the actual clet
        // result (cancellation) is incidental.
        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        int exit = await root.InvokeAsync (["select", "--initial", atLimit], cts.Token, stdout, stderr);

        Assert.NotEqual (ExitCodes.ValidationError, exit);
        Assert.DoesNotContain ("input-too-large", stderr.ToString ());
    }

    [Theory]
    [InlineData ("color", "not-a-color")]
    [InlineData ("int", "abc")]
    [InlineData ("decimal", "xyz")]
    [InlineData ("date", "not-a-date")]
    [InlineData ("time", "not-a-time")]
    [InlineData ("duration", "not-a-duration")]
    [InlineData ("confirm", "maybe")]
    // `linear-range` doesn't validate --initial strictly — unmatched labels just produce
    // an empty span. No "invalid --initial value" path to test, so it isn't in this list.
    public async Task Alias_InvalidInitialValue_ExitsWithUsageError (string alias, string initial)
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync ([alias, "--initial", initial], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("invalid --initial value", stderr.ToString ());
    }

    [Fact]
    public async Task MdCat_WithInitial_RendersToStdoutAndExitsOk ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["md", "--cat", "--initial", "# Hello"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.Ok, exit);
        Assert.NotEmpty (stdout.ToString ());
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public async Task MdCat_WithFile_RendersFileToStdout ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();
        string tempFile = Path.Combine (Path.GetTempPath (), $"clet-test-{Guid.NewGuid ()}.md");

        try
        {
            File.WriteAllText (tempFile, "# Test File\n\nSome content.");

            int exit = await root.InvokeAsync (["md", "--cat", "--allow-file", tempFile, tempFile], CancellationToken.None, stdout, stderr);

            Assert.Equal (ExitCodes.Ok, exit);
            Assert.NotEmpty (stdout.ToString ());
        }
        finally
        {
            File.Delete (tempFile);
        }
    }

    [Fact]
    public async Task MdCat_NoContent_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["md", "--cat"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--cat requires content", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_OutputMissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "--output"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--output", stderr.ToString ());
    }

    [Fact]
    public async Task Alias_ShortOutputMissingValue_ExitsWithUsageError ()
    {
        (CommandLineRoot root, StringWriter stdout, StringWriter stderr) = Build ();

        int exit = await root.InvokeAsync (["select", "-o"], CancellationToken.None, stdout, stderr);

        Assert.Equal (ExitCodes.UsageError, exit);
        Assert.Contains ("--output", stderr.ToString ());
    }
}
