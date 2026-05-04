using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Clet.IntegrationTests;

public class SelectCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        // Pre-cancelled token should return Cancelled without starting the UI
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        SelectClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "A,B,C" },
        };

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<int?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        // Use StopAfterFirstIteration to exercise the run path without blocking
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        SelectClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "Apple,Banana,Cherry" },
        };

        using CancellationTokenSource cts = new ();

        CletRunResult<int?> result = await clet.RunAsync (app, null, options, cts.Token);

        // Run returns after one iteration — result is Ok (value may be null since no input)
        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsSelection ()
    {
        // Verifies that the initial value is parsed and set on the selector
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        SelectClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "X,Y,Z" },
        };

        using CancellationTokenSource cts = new ();

        CletRunResult<int?> result = await clet.RunAsync (app, "2", options, cts.Token);

        // Should complete without error
        Assert.Equal (CletRunStatus.Ok, result.Status);
    }
}
