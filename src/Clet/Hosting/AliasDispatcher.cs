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

        // --cat mode: render viewer content directly to stdout without TUI
        if (options.Cat && clet is IViewerClet)
        {
            string? markdown = ResolveViewerContent (initial, options);

            if (markdown is null)
            {
                stderr.WriteLine ("error: --cat requires content via file arguments, --initial, or stdin.");

                return ExitCodes.UsageError;
            }

            MarkdownHelpRenderer.RenderToAnsi (markdown, stdout);

            return ExitCodes.Ok;
        }

        BoxedCletResult result;

        {
            ConfigurationManager.Enable (ConfigLocations.All);

            bool useFullscreen = options.Fullscreen || clet.Kind == CletKind.Viewer;
            Application.AppModel = useFullscreen ? AppModel.FullScreen : AppModel.Inline;

            using IApplication app = Application.Create ();
            app.Init ();

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
        }

        OutputFormatter.Write (result, options.JsonOutput, stdout, stderr);

        return ExitCodes.FromResult (result);
    }

    /// <summary>
    /// Resolves markdown content for --cat mode from file arguments, initial value, or stdin.
    /// </summary>
    private static string? ResolveViewerContent (string? initial, CletRunOptions options)
    {
        if (options.Arguments is { Count: > 0 } args)
        {
            List<string> contents = [];

            foreach (string arg in args)
            {
                if (File.Exists (arg))
                {
                    contents.Add (File.ReadAllText (arg));
                }
            }

            return contents.Count > 0 ? string.Join ("\n\n", contents) : null;
        }

        if (!string.IsNullOrEmpty (initial))
        {
            return initial;
        }

        if (Console.IsInputRedirected)
        {
            string stdinContent = Console.In.ReadToEnd ();

            return string.IsNullOrEmpty (stdinContent) ? null : stdinContent;
        }

        return null;
    }
}
