using System.Globalization;
using System.Text;

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
                stdout.WriteLine (GetVersion ());

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
        bool jsonOutput = false;
        bool fullscreen = false;
        TimeSpan? timeout = null;
        Dictionary<string, string> cletOptions = new (StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args [i];

            if (arg == "--json")
            {
                jsonOutput = true;

                continue;
            }

            if (arg == "--fullscreen")
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

            if (arg == "--initial")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine ("error: --initial requires a value.");

                    return ExitCodes.UsageError;
                }

                initial = args [++i];

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

            if (initial is null)
            {
                initial = arg;

                continue;
            }

            stderr.WriteLine ($"error: unexpected positional argument '{arg}'.");

            return ExitCodes.UsageError;
        }

        CletRunOptions options = new ()
        {
            JsonOutput = jsonOutput,
            Fullscreen = fullscreen,
            Timeout = timeout,
            CletOptions = cletOptions,
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

        StringBuilder sb = new ();
        sb.Append ("clet ").AppendLine (clet.PrimaryAlias);
        sb.AppendLine ();
        sb.AppendLine (clet.Description);
        sb.AppendLine ();
        sb.AppendLine ("Kind: " + (clet.Kind == CletKind.Input ? "input" : "viewer"));
        sb.AppendLine ("Result type: " + ResultTypeName (clet.ResultType));

        if (clet.Options.Count > 0)
        {
            sb.AppendLine ();
            sb.AppendLine ("Options:");

            foreach (CletOptionDescriptor opt in clet.Options)
            {
                string required = opt.Required ? " (required)" : string.Empty;
                string defaultPart = opt.DefaultValue is null ? string.Empty : $" [default: {opt.DefaultValue}]";
                sb.AppendLine ($"  --{opt.Name}{(opt.ShortName is null ? string.Empty : ", -" + opt.ShortName)}  {opt.Description}{required}{defaultPart}");
            }
        }

        stdout.Write (sb.ToString ());

        return ExitCodes.Ok;
    }

    private int WriteList (string[] args, TextWriter stdout)
    {
        bool json = args.Length > 1 && args [1] == "--json";

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
        stdout.WriteLine ("clet — typed terminal prompts (and viewers) for shells, scripts, and AI agents");
        stdout.WriteLine ();
        stdout.WriteLine ("Usage:");
        stdout.WriteLine ("  clet <alias> [initial] [--json] [--timeout <duration>] [--fullscreen] [--<opt> <value>]...");
        stdout.WriteLine ("  clet list [--json]");
        stdout.WriteLine ("  clet help <alias>");
        stdout.WriteLine ("  clet --help");
        stdout.WriteLine ("  clet --version");
        stdout.WriteLine ();
        stdout.WriteLine ("Available clets:");

        foreach (IClet clet in _registry.All)
        {
            stdout.WriteLine ($"  {clet.PrimaryAlias,-18}{clet.Description}");
        }

        stdout.WriteLine ();
        stdout.WriteLine ("Markdown-rendered help: v0.5.");
    }

    private static string GetVersion ()
    {
        Version? version = typeof (Program).Assembly.GetName ().Version;

        return version?.ToString (3) ?? "0.0.0";
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
