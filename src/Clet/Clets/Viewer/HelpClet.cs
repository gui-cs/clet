using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class HelpClet : IViewerClet
{
    private readonly ICletRegistry _registry;

    public HelpClet (ICletRegistry registry) => _registry = registry;

    public string PrimaryAlias => "help";
    public IReadOnlyList<string> Aliases => ["help"];
    public string Description => "Shows help for clet commands.";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof (void);
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CletOptionDescriptor> Options => [];

    public async Task<CletRunResult> RunAsync (
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        string? alias = options.Arguments?.FirstOrDefault ();
        (string markdown, string title) = BuildHelpContent (alias);

        // --cat mode: render to stdout and exit
        if (options.Cat)
        {
            MarkdownHelpRenderer.RenderToAnsi (markdown, Console.Out);

            return new () { Status = CletRunStatus.Ok };
        }

        // --- Build TUI ---

        Runnable window = new ()
        {
            Title = title,
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        Markdown markdownView = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1),
            SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus),
        };

        markdownView.ViewportSettings |= ViewportSettingsFlags.HasHorizontalScrollBar;

        Shortcut statusShortcut = new (Key.Empty, title, null) { MouseHighlightStates = MouseState.None };

        markdownView.LinkClicked += (_, e) =>
        {
            if (e.Url.StartsWith ("clet:help", StringComparison.OrdinalIgnoreCase))
            {
                string? linkAlias = e.Url.Length > "clet:help:".Length
                    ? e.Url ["clet:help:".Length..]
                    : null;

                (string md, string t) = BuildHelpContent (linkAlias);
                markdownView.Text = md;
                window.Title = t;
                statusShortcut.Title = t;
                e.Handled = true;

                return;
            }

            statusShortcut.Title = e.Url;
            e.Handled = true;
        };

        StatusBar statusBar = new ([
            new (Application.GetDefaultKey (Command.Quit), "Quit", window.RequestStop),
            statusShortcut,
        ])
        {
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast,
        };

        window.Add (markdownView, statusBar);

        window.Initialized += (_, _) =>
        {
            markdownView.Text = markdown;
        };

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        return new () { Status = CletRunStatus.Ok };
    }

    private (string Markdown, string Title) BuildHelpContent (string? alias)
    {
        if (alias is null)
        {
            return BuildOverview ();
        }

        if (alias is "help")
        {
            string helpMd = "# clet help\n\nNavigating clet's built-in help system.\n\n";
            string? helpExtra = MarkdownHelpRenderer.ReadEmbeddedHelp ("help.md");

            if (helpExtra is not null)
            {
                helpMd += helpExtra;
            }

            helpMd += "\n\n---\n\n[Back to overview](clet:help)\n";

            return (helpMd, "clet help");
        }

        if (!_registry.TryResolve (alias, out IClet? clet) || clet is null)
        {
            return ($"# Unknown clet: {alias}\n\nTry `clet list` to see available clets.", "clet help");
        }

        string md = MarkdownHelpRenderer.BuildAliasHelpMarkdown (clet);
        md += "\n\n---\n\n[Back to overview](clet:help)\n";

        return (md, $"clet help {clet.PrimaryAlias}");
    }

    private (string Markdown, string Title) BuildOverview ()
    {
        string? rawMarkdown = MarkdownHelpRenderer.ReadEmbeddedHelp ("overview.md");

        if (rawMarkdown is null)
        {
            return ("# clet\n\nNo overview available.", "clet");
        }

        string cletTable = MarkdownHelpRenderer.BuildCletTableMarkdown (_registry).TrimEnd ();
        string markdown = rawMarkdown.Replace ("{{CLET_TABLE}}", cletTable);
        markdown = markdown.Replace ("{{VERSION}}", $"v{GetVersion ()} (Terminal.Gui {GetTerminalGuiVersion ()})");

        return (markdown, "clet");
    }

    private static string GetVersion ()
    {
        string? informational = typeof (Program).Assembly
            .GetCustomAttributes (typeof (System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute> ()
            .FirstOrDefault ()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace (informational))
        {
            int plus = informational.IndexOf ('+');

            return plus >= 0 ? informational [..plus] : informational;
        }

        return typeof (Program).Assembly.GetName ().Version?.ToString (3) ?? "0.0.0";
    }

    private static string GetTerminalGuiVersion ()
    {
        System.Reflection.Assembly tg = typeof (Application).Assembly;
        string? informational = tg
            .GetCustomAttributes (typeof (System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute> ()
            .FirstOrDefault ()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace (informational))
        {
            int plus = informational.IndexOf ('+');

            return plus >= 0 ? informational [..plus] : informational;
        }

        return tg.GetName ().Version?.ToString (3) ?? "unknown";
    }
}
