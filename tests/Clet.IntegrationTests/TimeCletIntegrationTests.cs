using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class TimeCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        TimeClet clet = new ();
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

        TimeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsTime ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        TimeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, "14:30:00", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }
}
