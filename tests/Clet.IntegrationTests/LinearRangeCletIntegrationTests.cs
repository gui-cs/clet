using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class LinearRangeCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        LinearRangeClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "A,B,C,D" },
        };

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOkOrNoResult ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        LinearRangeClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "10,20,30,40,50" },
        };

        using CancellationTokenSource cts = new ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, null, options, cts.Token);

        // Without user input, the result is either Ok (with empty/initial selection)
        // or NoResult (if the LinearRange ended up with Kind=None). Both are acceptable
        // shapes for this integration test — we're verifying lifecycle, not content.
        Assert.True (result.Status is CletRunStatus.Ok or CletRunStatus.NoResult);
    }

    [Fact]
    public async Task RunAsync_WithInitialAndKind_StopsCleanly ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        LinearRangeClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string>
            {
                ["options"] = "S,M,L,XL",
                ["kind"] = "closed",
            },
        };

        using CancellationTokenSource cts = new ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            app, "S..L", options, cts.Token);

        Assert.True (result.Status is CletRunStatus.Ok or CletRunStatus.NoResult);
    }
}
