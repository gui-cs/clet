using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class DecimalCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        DecimalClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<decimal?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        DecimalClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<decimal?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsValue ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        DecimalClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<decimal?> result = await clet.RunAsync (app, "3.14", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInvalidInitialValue_ReturnsUsageError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        DecimalClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<decimal?> result = await clet.RunAsync (app, "not-a-decimal", options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("usage", result.ErrorCode);
        Assert.NotNull (result.ErrorMessage);
        Assert.Contains ("not-a-decimal", result.ErrorMessage);
    }
}
