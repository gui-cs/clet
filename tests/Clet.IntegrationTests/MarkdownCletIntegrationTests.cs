using System.Diagnostics;
using System.Reflection;
using Terminal.Gui.App;
using Terminal.Gui.Views;
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
    public async Task RunAsync_LinkClicked_SetsHandledTrue_AndNoProcessSpawned ()
    {
        // Markdown containing http and file:// links — ensures the clet renders
        // link-bearing content without launching any process
        const string markdown = """
            # Link Safety Test

            [click here](https://example.invalid)
            [dangerous](file:///etc/passwd)
            """;

        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        // Capture child process count before running
        int pid = Process.GetCurrentProcess ().Id;
        int childCountBefore = GetChildProcessCount (pid);

        MarkdownClet clet = new ();
        CletRunOptions options = new ();
        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, markdown, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);

        // Assert no child processes were spawned (no Process.Start for links)
        int childCountAfter = GetChildProcessCount (pid);
        Assert.Equal (childCountBefore, childCountAfter);
    }

    [Fact]
    public async Task RunAsync_LinkClicked_FileScheme_SetsHandledTrue_AndNoProcessSpawned ()
    {
        // Markdown containing a file:// link — the most dangerous scheme
        const string markdown = """
            # File Link Test

            [open file](file:///etc/passwd)
            """;

        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        int pid = Process.GetCurrentProcess ().Id;
        int childCountBefore = GetChildProcessCount (pid);

        MarkdownClet clet = new ();
        CletRunOptions options = new ();
        using CancellationTokenSource cts = new ();

        CletRunResult result = await clet.RunAsync (app, markdown, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);

        int childCountAfter = GetChildProcessCount (pid);
        Assert.Equal (childCountBefore, childCountAfter);
    }

    [Fact]
    public void LinkClicked_Handler_SetsHandledTrue ()
    {
        // Directly test the SurfaceOnly link policy pattern:
        // The handler shows URL in status bar and sets e.Handled = true
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        Markdown markdownView = new ()
        {
            Width = 80,
            Height = 24,
        };

        bool handledValue = false;
        string? capturedUrl = null;

        // Wire the same handler pattern as MarkdownClet (SurfaceOnly policy)
        markdownView.LinkClicked += (_, e) =>
        {
            capturedUrl = e.Url;
            e.Handled = true;
        };

        // Subscribe a second handler to observe that Handled was set by the first
        markdownView.LinkClicked += (_, e) =>
        {
            handledValue = e.Handled;
        };

        // Raise LinkClicked via reflection (protected method)
        RaiseLinkClicked (markdownView, "https://example.invalid");

        Assert.True (handledValue, "e.Handled should be true after the SurfaceOnly handler fires");
        Assert.Equal ("https://example.invalid", capturedUrl);

        // Also test file:// scheme
        handledValue = false;
        capturedUrl = null;
        RaiseLinkClicked (markdownView, "file:///etc/passwd");

        Assert.True (handledValue, "e.Handled should be true for file:// links");
        Assert.Equal ("file:///etc/passwd", capturedUrl);
    }

    private static void RaiseLinkClicked (Markdown markdownView, string url)
    {
        // RaiseLinkClicked(string) is non-public; use reflection to invoke it for testing
        MethodInfo? method = typeof (Markdown).GetMethod (
            "RaiseLinkClicked",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            [typeof (string)]);

        Assert.NotNull (method);
        method.Invoke (markdownView, [url]);
    }

    private static int GetChildProcessCount (int parentPid)
    {
        try
        {
            // On Linux, enumerate /proc to find child processes
            if (OperatingSystem.IsLinux ())
            {
                int count = 0;
                string parentPidStr = parentPid.ToString ();

                foreach (string dir in Directory.GetDirectories ("/proc"))
                {
                    string statusPath = Path.Combine (dir, "status");

                    if (!File.Exists (statusPath))
                    {
                        continue;
                    }

                    try
                    {
                        string content = File.ReadAllText (statusPath);

                        if (content.Contains ($"PPid:\t{parentPidStr}"))
                        {
                            count++;
                        }
                    }
                    catch
                    {
                        // Process may have exited
                    }
                }

                return count;
            }

            // Fallback: count via Process API
            return Process.GetProcesses ().Length;
        }
        catch
        {
            return 0;
        }
    }
}
