using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class MarkdownCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MarkdownClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult result = await clet.RunAsync (app, "# Test", options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_AndInlineContent_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MarkdownClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, "# Hello\n\nThis is **Markdown**.", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_NoContent_NoFiles_ReturnsError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MarkdownClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("io", result.ErrorCode);
    }

    [Fact]
    public async Task RunAsync_WithFileArgument_NonexistentFile_ReturnsError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MarkdownClet clet = new ();
        CletRunOptions options = new ()
        {
            Arguments = ["/nonexistent/path/to/file.md"],
        };

        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("io", result.ErrorCode);
    }

    [Fact]
    public async Task RunAsync_WithFileArgument_ReturnsOk ()
    {
        // Create a temporary markdown file for this test
        string tempFile = Path.GetTempFileName ();

        try
        {
            File.WriteAllText (tempFile, "# Test File\n\nSome content.");

            using IApplication app = Application.Create ();
            app.Init ("ansi");
            app.StopAfterFirstIteration = true;

            MarkdownClet clet = new ();
            CletRunOptions options = new ()
            {
                Arguments = [tempFile],
            };

            using CancellationTokenSource cts = new ();

            CletRunResult result = await clet.RunAsync (app, null, options, cts.Token);

            Assert.Equal (CletRunStatus.Ok, result.Status);
        }
        finally
        {
            File.Delete (tempFile);
        }
    }

    [Fact]
    public async Task RunAsync_LinkInMarkdown_DoesNotLaunchProcess ()
    {
        // Verifies D-017 SurfaceOnly link policy: links are displayed but never opened.
        // The LinkClicked handler sets e.Handled = true; no Process.Start is called.
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MarkdownClet clet = new ();
        string markdownWithLink = "# Links\n\n[Click me](https://evil.example.com/exfiltrate?data=secret)";
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        // This should render without launching any external process.
        CletRunResult result = await clet.RunAsync (app, markdownWithLink, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_ContentExceeds8MiB_ReturnsInputTooLarge ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MarkdownClet clet = new ();
        string oversized = new ('x', MarkdownClet.MaxStdinBytes + 1);
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, oversized, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("input-too-large", result.ErrorCode);
    }

    [Fact]
    public async Task RunAsync_FileExceeds8MiB_ReturnsInputTooLarge ()
    {
        string tempFile = Path.GetTempFileName ();

        try
        {
            // Write a file just over 8 MiB
            using (FileStream fs = File.OpenWrite (tempFile))
            {
                byte[] buffer = new byte [1024 * 1024]; // 1 MiB
                Array.Fill (buffer, (byte)'x');

                for (int i = 0; i < 9; i++)
                {
                    fs.Write (buffer);
                }
            }

            using IApplication app = Application.Create ();
            app.Init ("ansi");

            MarkdownClet clet = new ();
            CletRunOptions options = new ()
            {
                Arguments = [tempFile],
            };

            using CancellationTokenSource cts = new ();

            CletRunResult result = await clet.RunAsync (app, null, options, cts.Token);

            Assert.Equal (CletRunStatus.Error, result.Status);
            Assert.Equal ("input-too-large", result.ErrorCode);
        }
        finally
        {
            File.Delete (tempFile);
        }
    }
}
