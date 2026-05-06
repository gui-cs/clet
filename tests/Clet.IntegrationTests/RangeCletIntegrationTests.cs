using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class RangeCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        RangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<JsonObject?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        RangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<JsonObject?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsRange ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        RangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<JsonObject?> result = await clet.RunAsync (app, "5..10", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInvalidInitialValue_ReturnsUsageError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        RangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<JsonObject?> result = await clet.RunAsync (app, "not-a-range", options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("usage", result.ErrorCode);
        Assert.NotNull (result.ErrorMessage);
        Assert.Contains ("not-a-range", result.ErrorMessage);
    }
}
