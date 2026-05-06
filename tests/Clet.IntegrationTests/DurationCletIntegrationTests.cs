using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class DurationCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        DurationClet clet = new ();
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

        DurationClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsDuration ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        DurationClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, "PT1H30M", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInvalidInitialValue_ReturnsUsageError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        DurationClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<string?> result = await clet.RunAsync (app, "not-a-duration", options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("usage", result.ErrorCode);
        Assert.NotNull (result.ErrorMessage);
        Assert.Contains ("not-a-duration", result.ErrorMessage);
    }
}
