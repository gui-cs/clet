using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class AttributePickerCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        AttributePickerClet clet = new ();
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

        AttributePickerClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<JsonObject?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }
}
