using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class PickFileCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        PickFileClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<JsonNode?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact (Skip = "FileDialog enumerates mount points during init, crashes under ansi driver. Covered by smoke tests at v0.3.")]
    public async Task RunAsync_WithStopAfterFirstIteration_CompletesWithoutError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        PickFileClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<JsonNode?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.True (result.Status == CletRunStatus.Ok || result.Status == CletRunStatus.Cancelled);
    }
}
