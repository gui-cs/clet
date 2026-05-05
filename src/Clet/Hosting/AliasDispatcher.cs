using Terminal.Gui.App;

namespace Clet;

internal sealed class AliasDispatcher
{
    private readonly ICletRegistry _registry;

    public AliasDispatcher (ICletRegistry registry) => _registry = registry;

    public async Task<int> DispatchAsync (
        string alias,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!_registry.TryResolve (alias, out IClet? clet) || clet is null)
        {
            stderr.WriteLine ($"error: unknown alias '{alias}'. Try 'clet list' to see available clets.");

            return ExitCodes.UsageError;
        }

        using CancellationTokenSource? timeoutSource = options.Timeout is { } timeout
            ? new (timeout)
            : null;
        using CancellationTokenSource linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource (cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutSource.Token);

        BoxedCletResult result;

        using IApplication app = Application.Create ();
        app.Init ("ansi");

        try
        {
            result = await clet.RunBoxedAsync (app, initial, options, linkedSource.Token);
        }
        catch (OperationCanceledException)
        {
            result = new (CletRunStatus.Cancelled, null, null, null);
        }
        catch (Exception ex)
        {
            result = new (CletRunStatus.Error, null, "io", ex.Message);
        }

        OutputFormatter.Write (result, options.JsonOutput, stdout, stderr);

        return ExitCodes.FromResult (result);
    }
}
