using Terminal.Gui.App;
using Xunit;

namespace Clet.IntegrationTests;

public class IntCletIntegrationTests
{
    [Fact]
    public async Task RunAsync_CancellationToken_AlreadyCancelled_ReturnsCancelled ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        IntClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<int?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
        Assert.Null (result.Value);
    }

    [Fact]
    public async Task RunAsync_WithStopAfterFirstIteration_ReturnsOk ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        IntClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<int?> result = await clet.RunAsync (app, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInitialValue_SetsValue ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");
        app.StopAfterFirstIteration = true;

        IntClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<int?> result = await clet.RunAsync (app, "42", options, cts.Token);

        Assert.Equal (CletRunStatus.Ok, result.Status);
    }

    [Fact]
    public async Task RunAsync_WithInvalidInitialValue_ReturnsUsageError ()
    {
        using IApplication app = Application.Create ();
        app.Init ("ansi");

        IntClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<int?> result = await clet.RunAsync (app, "not-an-int", options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("usage", result.ErrorCode);
        Assert.NotNull (result.ErrorMessage);
        Assert.Contains ("not-an-int", result.ErrorMessage);
    }
}
