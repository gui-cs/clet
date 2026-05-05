using System.Reflection;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;

namespace Clet;

/// <summary>
/// Renders Markdown content as ANSI to a <see cref="TextWriter"/> (print mode).
/// Adapted from mdv's RenderMarkdown() print pipeline.
/// </summary>
internal static class MarkdownHelpRenderer
{
    /// <summary>
    /// Renders the given markdown string to ANSI escape sequences and writes them to <paramref name="output"/>.
    /// </summary>
    public static void RenderToAnsi (string markdown, TextWriter output)
    {
        int width;

        try
        {
            width = Console.WindowWidth;
        }
        catch
        {
            width = 0;
        }

        if (width <= 0)
        {
            width = 80;
        }

        int height;

        try
        {
            height = Console.WindowHeight;
        }
        catch
        {
            height = 0;
        }

        if (height <= 0)
        {
            height = 24;
        }

        // Prevent the ANSI driver from trying to read/write real terminal size or capabilities,
        // since we're just emitting ANSI and exiting immediately.
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
        IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);

        try
        {
            app.Driver?.SetScreenSize (width, height);

            Markdown markdownView = new ()
            {
                App = app,
                SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus),
                UseThemeBackground = false,
                ShowCopyButtons = false,
                Width = Dim.Fill (),
                Height = Dim.Fill (),
                Text = markdown,
            };

            // Layout to get natural content height
            markdownView.SetRelativeLayout (app.Screen.Size);
            markdownView.Layout ();

            // Resize to the full content height but keep the terminal width
            int contentHeight = markdownView.GetContentHeight ();
            app.Driver?.SetScreenSize (width, contentHeight);
            markdownView.SetRelativeLayout (app.Screen.Size);
            markdownView.Frame = app.Screen with { X = 0, Y = 0 };
            markdownView.Layout ();

            app.Driver?.ClearContents ();
            markdownView.Draw ();
            output.WriteLine (app.Driver?.ToAnsi ());
        }
        finally
        {
            app.Dispose ();
        }
    }

    /// <summary>
    /// Reads an embedded markdown resource from the <c>Help/</c> directory.
    /// Returns <c>null</c> if the resource is not found.
    /// </summary>
    public static string? ReadEmbeddedHelp (string resourceSuffix)
    {
        Assembly assembly = typeof (MarkdownHelpRenderer).Assembly;
        string? resourceName = assembly.GetManifestResourceNames ()
            .FirstOrDefault (n => n.EndsWith (resourceSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            return null;
        }

        using Stream stream = assembly.GetManifestResourceStream (resourceName)!;
        using StreamReader reader = new (stream);

        return reader.ReadToEnd ();
    }

    /// <summary>
    /// Generates a Markdown string for a clet's help page from its <see cref="IClet"/> metadata.
    /// </summary>
    public static string BuildAliasHelpMarkdown (IClet clet)
    {
        StringBuilder sb = new ();
        sb.AppendLine ($"# clet {clet.PrimaryAlias}");
        sb.AppendLine ();
        sb.AppendLine (clet.Description);
        sb.AppendLine ();
        sb.AppendLine ($"**Kind:** {(clet.Kind == CletKind.Input ? "input" : "viewer")}");
        sb.AppendLine ();
        sb.AppendLine ($"**Result type:** {ResultTypeName (clet.ResultType)}");

        if (clet.Aliases.Count > 1)
        {
            sb.AppendLine ();
            sb.AppendLine ($"**Aliases:** {string.Join (", ", clet.Aliases.Select (a => $"`{a}`"))}");
        }

        if (clet.Options.Count > 0)
        {
            sb.AppendLine ();
            sb.AppendLine ("## Options");
            sb.AppendLine ();
            sb.AppendLine ("| Option | Type | Description | Required | Default |");
            sb.AppendLine ("|--------|------|-------------|----------|---------|");

            foreach (CletOptionDescriptor opt in clet.Options)
            {
                string name = opt.ShortName is null
                    ? $"`--{opt.Name}`"
                    : $"`--{opt.Name}`, `-{opt.ShortName}`";
                string required = opt.Required ? "yes" : "no";
                string defaultVal = opt.DefaultValue ?? "-";
                sb.AppendLine ($"| {name} | {ResultTypeName (opt.ValueType)} | {opt.Description} | {required} | {defaultVal} |");
            }
        }

        return sb.ToString ();
    }

    /// <summary>
    /// Generates a Markdown table of all registered clets with aliases shown inline.
    /// </summary>
    public static string BuildCletTableMarkdown (ICletRegistry registry)
    {
        StringBuilder sb = new ();
        sb.AppendLine ("## Available Clets");
        sb.AppendLine ();
        sb.AppendLine ("| Alias | Description |");
        sb.AppendLine ("|-------|-------------|");

        foreach (IClet clet in registry.All)
        {
            string aliases = clet.Aliases.Count <= 1
                ? $"`{clet.PrimaryAlias}`"
                : string.Join (", ", clet.Aliases.Select (a => $"`{a}`"));

            sb.AppendLine ($"| {aliases} | {clet.Description} |");
        }

        return sb.ToString ();
    }

    private static string ResultTypeName (Type type)
    {
        Type underlying = Nullable.GetUnderlyingType (type) ?? type;

        if (underlying == typeof (string)) return "string";
        if (underlying == typeof (int) || underlying == typeof (long) || underlying == typeof (short)) return "int";
        if (underlying == typeof (decimal) || underlying == typeof (double) || underlying == typeof (float)) return "decimal";
        if (underlying == typeof (bool)) return "bool";
        if (underlying == typeof (DateTime) || underlying == typeof (DateOnly)) return "date";
        if (underlying == typeof (TimeOnly)) return "time";
        if (underlying == typeof (TimeSpan)) return "duration";
        if (underlying == typeof (void)) return "none";

        return underlying.Name;
    }
}
