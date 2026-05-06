using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class ConfirmCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        ConfirmClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<bool?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        ConfirmClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<bool?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValueTrue_SetsYes ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        ConfirmClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<bool?> result = await clet.RunAsync (app, "true", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInvalidInitialValue_ReturnsUsageError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        ConfirmClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<bool?> result = await clet.RunAsync (app, "maybe", options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("usage", result.ErrorCode);
        Assert.NotNull (result.ErrorMessage);
        Assert.Contains ("maybe", result.ErrorMessage);
    }
}
