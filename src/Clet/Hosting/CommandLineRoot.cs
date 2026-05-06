using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Clet;

internal sealed class CommandLineRoot
{
    private readonly ICletRegistry _registry;
    private readonly AliasDispatcher _dispatcher;

    public CommandLineRoot (ICletRegistry registry)
    {
        _registry = registry;
        _dispatcher = new (registry);
    }

    public async Task<int> InvokeAsync (
        string[] args,
        CancellationToken cancellationToken,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (args.Length == 0)
        {
            WriteRootHelp (stdout);

            return ExitCodes.Ok;
        }

        switch (args [0])
        {
            case "--help":
            case "-h":
                WriteRootHelp (stdout);

                return ExitCodes.Ok;

            case "--version":
                stdout.WriteLine ($"{GetVersion ()} (Terminal.Gui {GetTerminalGuiVersion ()})");

                return ExitCodes.Ok;

            case "help":
                return WriteAliasHelp (args, stdout, stderr);

            case "list":
                return WriteList (args, stdout);
        }

        return await DispatchAlias (args, cancellationToken, stdout, stderr);
    }

    private async Task<int> DispatchAlias (
        string[] args,
        CancellationToken cancellationToken,
        TextWriter stdout,
        TextWriter stderr)
    {
        string alias = args [0];
        string? initial = null;
        string? title = null;
        bool jsonOutput = false;
        bool fullscreen = false;
        TimeSpan? timeout = null;
        Dictionary<string, string> cletOptions = new (StringComparer.OrdinalIgnoreCase);
        List<string> positionalArgs = [];

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args [i];

            if (arg is "--json" or "-j")
            {
                jsonOutput = true;

                continue;
            }

            if (arg is "--fullscreen" or "-f")
            {
                fullscreen = true;

                continue;
            }

            if (arg == "--timeout")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine ("error: --timeout requires a value (e.g. 30s, 500ms).");

                    return ExitCodes.UsageError;
                }

                if (!TryParseTimeout (args [++i], out TimeSpan parsed))
                {
                    stderr.WriteLine ($"error: invalid --timeout value '{args [i]}'. Use 30s, 1m, 500ms.");

                    return ExitCodes.UsageError;
                }

                timeout = parsed;

                continue;
            }

            if (arg is "--initial" or "-i")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine ("error: --initial requires a value.");

                    return ExitCodes.UsageError;
                }

                initial = args [++i];

                continue;
            }

            if (arg is "--title" or "-t")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine ("error: --title requires a value.");

                    return ExitCodes.UsageError;
                }

                title = args [++i];

                continue;
            }

            if (arg.StartsWith ("--", StringComparison.Ordinal))
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine ($"error: option '{arg}' requires a value.");

                    return ExitCodes.UsageError;
                }

                cletOptions [arg [2..]] = args [++i];

                continue;
            }

            positionalArgs.Add (arg);
        }

        CletRunOptions options = new ()
        {
            JsonOutput = jsonOutput,
            Fullscreen = fullscreen,
            Timeout = timeout,
            Title = title,
            CletOptions = cletOptions,
            Arguments = positionalArgs.Count > 0 ? positionalArgs : null,
        };

        return await _dispatcher.DispatchAsync (alias, initial, options, cancellationToken, stdout, stderr);
    }

    private int WriteAliasHelp (string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length < 2)
        {
            WriteRootHelp (stdout);

            return ExitCodes.Ok;
        }

        string alias = args [1];

        if (!_registry.TryResolve (alias, out IClet? clet) || clet is null)
        {
            stderr.WriteLine ($"error: unknown alias '{alias}'. Try 'clet list' to see available clets.");

            return ExitCodes.UsageError;
        }

        string markdown = MarkdownHelpRenderer.BuildAliasHelpMarkdown (clet);
        MarkdownHelpRenderer.RenderToAnsi (markdown, stdout);

        return ExitCodes.Ok;
    }

    private int WriteList (string[] args, TextWriter stdout)
    {
        bool json = args.Length > 1 && args [1] is "--json" or "-j";

        if (json)
        {
            stdout.WriteLine (BuildListJson ());

            return ExitCodes.Ok;
        }

        foreach (IClet clet in _registry.All)
        {
            stdout.WriteLine ($"{clet.PrimaryAlias} - {clet.Description}");
        }

        return ExitCodes.Ok;
    }

    private string BuildListJson ()
    {
        // Hand-built JSON to keep AOT-friendly without a list-specific JsonSerializerContext entry.
        StringBuilder sb = new ();
        sb.Append ("{\"schemaVersion\":1,\"clets\":[");

        bool first = true;

        foreach (IClet clet in _registry.All)
        {
            if (!first)
            {
                sb.Append (',');
            }

            first = false;

            sb.Append ("{\"alias\":");
            AppendJsonString (sb, clet.PrimaryAlias);
            sb.Append (",\"aliases\":[");
            bool firstAlias = true;

            foreach (string alias in clet.Aliases)
            {
                if (!firstAlias)
                {
                    sb.Append (',');
                }

                firstAlias = false;
                AppendJsonString (sb, alias);
            }

            sb.Append ("],\"description\":");
            AppendJsonString (sb, clet.Description);
            sb.Append (",\"kind\":");
            AppendJsonString (sb, clet.Kind == CletKind.Input ? "input" : "viewer");
            sb.Append (",\"resultType\":");
            AppendJsonString (sb, ResultTypeName (clet.ResultType));
            sb.Append (",\"options\":[");
            bool firstOpt = true;

            foreach (CletOptionDescriptor opt in clet.Options)
            {
                if (!firstOpt)
                {
                    sb.Append (',');
                }

                firstOpt = false;
                sb.Append ("{\"name\":");
                AppendJsonString (sb, opt.Name);

                if (opt.ShortName is not null)
                {
                    sb.Append (",\"shortName\":");
                    AppendJsonString (sb, opt.ShortName);
                }

                sb.Append (",\"valueType\":");
                AppendJsonString (sb, ResultTypeName (opt.ValueType));
                sb.Append (",\"description\":");
                AppendJsonString (sb, opt.Description);
                sb.Append (",\"required\":");
                sb.Append (opt.Required ? "true" : "false");

                if (opt.DefaultValue is not null)
                {
                    sb.Append (",\"defaultValue\":");
                    AppendJsonString (sb, opt.DefaultValue);
                }

                sb.Append ('}');
            }

            sb.Append ("]}");
        }

        sb.Append ("]}");

        return sb.ToString ();
    }

    private static void AppendJsonString (StringBuilder sb, string value)
    {
        sb.Append ('"');

        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append ("\\\""); break;
                case '\\': sb.Append ("\\\\"); break;
                case '\b': sb.Append ("\\b"); break;
                case '\f': sb.Append ("\\f"); break;
                case '\n': sb.Append ("\\n"); break;
                case '\r': sb.Append ("\\r"); break;
                case '\t': sb.Append ("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append ("\\u").Append (((int)c).ToString ("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append (c);
                    }

                    break;
            }
        }

        sb.Append ('"');
    }

    private void WriteRootHelp (TextWriter stdout)
    {
        string? markdown = MarkdownHelpRenderer.ReadEmbeddedHelp ("overview.md");

        if (markdown is null)
        {
            // Fallback to plain text if resource not found
            stdout.WriteLine ("clet — typed terminal prompts (and viewers) for shells, scripts, and AI agents");
            stdout.WriteLine ();
            stdout.WriteLine ("Usage: clet <alias> [options]");
            stdout.WriteLine ("       clet list [--json]");
            stdout.WriteLine ("       clet help <alias>");

            return;
        }

        // Inject dynamic content into the template
        string cletTable = MarkdownHelpRenderer.BuildCletTableMarkdown (_registry).TrimEnd ();
        markdown = markdown.Replace ("{{CLET_TABLE}}", cletTable);
        markdown = markdown.Replace ("{{VERSION}}", $"v{GetVersion ()} (Terminal.Gui {GetTerminalGuiVersion ()})");

        MarkdownHelpRenderer.RenderToAnsi (markdown, stdout);
    }

    private static string GetVersion ()
    {
        Version? version = typeof (Program).Assembly.GetName ().Version;

        return version?.ToString (3) ?? "0.0.0";
    }

    private static string GetTerminalGuiVersion ()
    {
        System.Reflection.Assembly tg = typeof (Terminal.Gui.App.Application).Assembly;
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

    private static string ResultTypeName (Type type)
    {
        Type underlying = Nullable.GetUnderlyingType (type) ?? type;

        if (underlying == typeof (string))
        {
            return "string";
        }

        if (underlying == typeof (int) || underlying == typeof (long) || underlying == typeof (short))
        {
            return "int";
        }

        if (underlying == typeof (decimal) || underlying == typeof (double) || underlying == typeof (float))
        {
            return "decimal";
        }

        if (underlying == typeof (bool))
        {
            return "bool";
        }

        if (underlying == typeof (DateTime) || underlying == typeof (DateOnly))
        {
            return "date";
        }

        if (underlying == typeof (TimeOnly))
        {
            return "time";
        }

        if (underlying == typeof (TimeSpan))
        {
            return "duration";
        }

        if (underlying == typeof (JsonArray))
        {
            return "array";
        }

        if (underlying == typeof (JsonObject))
        {
            return "object";
        }

        if (underlying == typeof (JsonNode))
        {
            return "json";
        }

        return underlying.Name;
    }

    public static bool TryParseTimeout (string input, out TimeSpan timeout)
    {
        timeout = default;

        if (string.IsNullOrEmpty (input))
        {
            return false;
        }

        // Supported suffixes: ms, s, m, h. Numeric body must be a positive integer or decimal.
        string body;
        Func<double, TimeSpan> factory;

        if (input.EndsWith ("ms", StringComparison.OrdinalIgnoreCase))
        {
            body = input [..^2];
            factory = TimeSpan.FromMilliseconds;
        }
        else if (input.EndsWith ("s", StringComparison.OrdinalIgnoreCase))
        {
            body = input [..^1];
            factory = TimeSpan.FromSeconds;
        }
        else if (input.EndsWith ("m", StringComparison.OrdinalIgnoreCase))
        {
            body = input [..^1];
            factory = TimeSpan.FromMinutes;
        }
        else if (input.EndsWith ("h", StringComparison.OrdinalIgnoreCase))
        {
            body = input [..^1];
            factory = TimeSpan.FromHours;
        }
        else
        {
            return false;
        }

        if (!double.TryParse (body, NumberStyles.Number, CultureInfo.InvariantCulture, out double value) || value <= 0)
        {
            return false;
        }

        timeout = factory (value);

        return true;
    }
}
