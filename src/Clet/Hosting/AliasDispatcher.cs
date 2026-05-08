using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

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

        if (initial is not null && !clet.TryValidateInitial (initial, options))
        {
            stderr.WriteLine ($"error: invalid --initial value '{initial}' for '{alias}'.");

            return ExitCodes.UsageError;
        }

        using CancellationTokenSource? timeoutSource = options.Timeout is { } timeout
            ? new (timeout)
            : null;
        using CancellationTokenSource linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource (cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutSource.Token);

        // --cat mode: render viewer content directly to stdout without TUI.
        if (options.Cat && clet is IViewerClet)
        {
            if (clet is HelpClet helpClet)
            {
                return helpClet.RenderCat (options, stdout, stderr);
            }

            string? markdown = ResolveViewerContent (initial, options, stderr);

            if (markdown is not null)
            {
                MarkdownHelpRenderer.RenderToAnsi (markdown, stdout);

                return ExitCodes.Ok;
            }
        }

        BoxedCletResult result;

        {
            try
            {
                Logging.Information ("AliasDispatcher: calling ConfigurationManager.Enable(All)");
                ConfigurationManager.Enable (ConfigLocations.All);
                Logging.Information ("AliasDispatcher: Enable(All) succeeded");
            }
            catch (Exception ex)
            {
                Logging.Error ($"AliasDispatcher: Enable(All) threw {ex.GetType ().Name}: {ex.Message}");

                // Bad config should not prevent clet from running — fall back to defaults
                ConfigurationManager.Disable (resetToHardCodedDefaults: true);
                ConfigurationManager.Enable (ConfigLocations.None);
                Logging.Information ("AliasDispatcher: fell back to hard-coded defaults");
            }

            bool useFullscreen = options.Fullscreen || clet.Kind == CletKind.Viewer;
            Application.AppModel = useFullscreen ? AppModel.FullScreen : AppModel.Inline;

            using IApplication app = Application.Create ();
            app.Init ();

            try
            {
                Logging.Information ("AliasDispatcher: calling RunBoxedAsync");
                result = await clet.RunBoxedAsync (app, initial, options, linkedSource.Token);
                Logging.Information ($"AliasDispatcher: RunBoxedAsync returned {result.Status}");
            }
            catch (OperationCanceledException)
            {
                Logging.Information ("AliasDispatcher: RunBoxedAsync was cancelled");
                result = new (CletRunStatus.Cancelled, null, null, null);
            }
            catch (Exception ex)
            {
                Logging.Error ($"AliasDispatcher: RunBoxedAsync threw {ex.GetType ().Name}: {ex.Message}\n{ex.StackTrace}");
                result = new (CletRunStatus.Error, null, "io", ex.Message);
            }
        }

        if (!OutputFormatter.Write (result, options.JsonOutput, stdout, stderr, options.OutputPath))
        {
            return ExitCodes.UsageError;
        }

        return ExitCodes.FromResult (result);
    }

    /// <summary>
    /// Resolves markdown content for --cat mode from file arguments, initial value, or stdin.
    /// </summary>
    private static string? ResolveViewerContent (string? initial, CletRunOptions options, TextWriter stderr)
    {
        TextReader? stdinReader = Console.IsInputRedirected ? Console.In : null;
        var result = MarkdownContentResolver.Resolve (initial, options, stdinReader);

        if (!result.IsSuccess)
        {
            stderr.WriteLine ($"error: {result.ErrorMessage}");

            return null;
        }

        return result.Content;
    }
}
