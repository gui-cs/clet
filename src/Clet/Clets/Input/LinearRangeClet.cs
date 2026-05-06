using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

/// <summary>
///     Presents a linear-range slider with discrete option stops; user picks one or two endpoints.
///     Supersedes the old <c>range</c> clet (NumericUpDown ×2). Backed by Terminal.Gui's
///     <see cref="LinearRange{T}"/> ([Terminal.Gui PR #5204](https://github.com/gui-cs/Terminal.Gui/pull/5204)).
/// </summary>
internal sealed class LinearRangeClet : IClet<JsonObject?>
{
    public string PrimaryAlias => "linear-range";
    public IReadOnlyList<string> Aliases => ["linear-range"];
    public string Description => "Pick one or two endpoints from a discrete option list (closed range, or left-/right-bounded).";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (JsonObject);

    /// <summary>Positional args become the option labels (alternative to --options). See D-025.</summary>
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("options", "o", typeof (string), "Comma-separated list of option labels (the discrete stops).", true, null),
        new ("kind", "k", typeof (string), "Range kind: closed | left-bounded | right-bounded. Default: closed.", false, "closed"),
    ];

    public async Task<CletRunResult<JsonObject?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        // Resolve options: positional args win, then --options, else error.
        string[] labels = options.Arguments is { Count: > 0 }
            ? LabelParser.Split (options.Arguments)
            : options.CletOptions?.TryGetValue ("options", out string? optionsValue) == true
                ? LabelParser.Split (optionsValue)
                : [];

        if (labels.Length == 0)
        {
            return new ()
            {
                Status = CletRunStatus.Error,
                ErrorCode = "validation",
                ErrorMessage = "linear-range requires --options or positional labels.",
            };
        }

        // Resolve --kind
        LinearRangeSpanKind kind = LinearRangeSpanKind.Closed;

        if (options.CletOptions?.TryGetValue ("kind", out string? kindStr) == true && kindStr is not null)
        {
            switch (kindStr.Trim ().ToLowerInvariant ())
            {
                case "closed":
                    kind = LinearRangeSpanKind.Closed;
                    break;
                case "left-bounded":
                case "left":
                    kind = LinearRangeSpanKind.LeftBounded;
                    break;
                case "right-bounded":
                case "right":
                    kind = LinearRangeSpanKind.RightBounded;
                    break;
                default:
                    return new ()
                    {
                        Status = CletRunStatus.Error,
                        ErrorCode = "validation",
                        ErrorMessage = $"linear-range: unknown --kind '{kindStr}'. Use closed | left-bounded | right-bounded.",
                    };
            }
        }

        LinearRange<string> view = new (labels.ToList ())
        {
            RangeKind = kind,
        };

        // --initial format: "low..high" for closed; "..end" for left-bounded; "start.." for right-bounded.
        if (initial is not null)
        {
            ApplyInitial (view, kind, initial, labels);
        }

        RunnableWrapper<LinearRange<string>, LinearRangeSpan<string>> wrapper = new (view)
        {
            Title = options.Title ?? "Pick a range (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            SchemeName = CletStyling.BaseSchemeName,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);

        try
        {
            await app.RunAsync (wrapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        LinearRangeSpan<string> result = wrapper.Result;

        // None or empty selection → no-result.
        if (result.Kind == LinearRangeSpanKind.None)
        {
            return new () { Status = CletRunStatus.NoResult };
        }

        return new () { Status = CletRunStatus.Ok, Value = ToJson (result) };
    }

    private static void ApplyInitial (LinearRange<string> view, LinearRangeSpanKind kind, string initial, string[] labels)
    {
        // Parse "low..high", "..end", or "start.."
        int sep = initial.IndexOf ("..", StringComparison.Ordinal);
        string? startLabel;
        string? endLabel;

        if (sep < 0)
        {
            // No separator: treat the whole string as a single bound. The kind decides which side.
            startLabel = kind == LinearRangeSpanKind.LeftBounded ? null : initial;
            endLabel = kind == LinearRangeSpanKind.RightBounded ? null : initial;
        }
        else
        {
            string left = initial.Substring (0, sep);
            string right = initial.Substring (sep + 2);
            startLabel = string.IsNullOrEmpty (left) ? null : left;
            endLabel = string.IsNullOrEmpty (right) ? null : right;
        }

        int startIdx = startLabel is null
            ? -1
            : Array.FindIndex (labels, l => string.Equals (l, startLabel, StringComparison.OrdinalIgnoreCase));
        int endIdx = endLabel is null
            ? -1
            : Array.FindIndex (labels, l => string.Equals (l, endLabel, StringComparison.OrdinalIgnoreCase));

        // Resolve back to the actual label strings (in case the caller used different casing).
        string? startMatch = startIdx >= 0 ? labels [startIdx] : null;
        string? endMatch = endIdx >= 0 ? labels [endIdx] : null;

        view.Value = new LinearRangeSpan<string> (kind, startMatch, endMatch, startIdx, endIdx);
    }

    private static JsonObject ToJson (LinearRangeSpan<string> span)
    {
        JsonObject obj = new ()
        {
            ["kind"] = KindToWire (span.Kind),
        };

        // For Closed: emit both start and end.
        // For LeftBounded: emit only end (everything ≤ end).
        // For RightBounded: emit only start (everything ≥ start).
        if (span.Kind == LinearRangeSpanKind.Closed || span.Kind == LinearRangeSpanKind.RightBounded)
        {
            obj ["start"] = span.Start;
        }

        if (span.Kind == LinearRangeSpanKind.Closed || span.Kind == LinearRangeSpanKind.LeftBounded)
        {
            obj ["end"] = span.End;
        }

        return obj;
    }

    private static string KindToWire (LinearRangeSpanKind kind) => kind switch
    {
        LinearRangeSpanKind.Closed => "closed",
        LinearRangeSpanKind.LeftBounded => "left-bounded",
        LinearRangeSpanKind.RightBounded => "right-bounded",
        _ => "none",
    };
}
