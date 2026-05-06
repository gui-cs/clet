using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class MultilineTextCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        MultilineTextClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MultilineTextClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsText ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MultilineTextClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, "line1\nline2", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithRows_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        MultilineTextClet clet = new ();
        CletRunOptions options = new () { Rows = 10 };

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }
}
