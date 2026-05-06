using System.Globalization;
using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class RangeClet : IClet<JsonObject?>
{
    public string PrimaryAlias => "range";
    public IReadOnlyList<string> Aliases => ["range"];
    public string Description => "Prompts for a numeric range (low..high) and returns a JSON object.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (JsonObject);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("step", null, typeof (int), "Step increment.", false, "1"),
    ];

    public bool TryValidateInitial (string initial, CletRunOptions options)
        => TryParseRange (initial, out _, out _);

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

        RangeView rangeView = new ();

        if (options.CletOptions?.TryGetValue ("step", out string? stepStr) == true
            && int.TryParse (stepStr, CultureInfo.InvariantCulture, out int step))
        {
            rangeView.Increment = step;
        }

        if (initial is not null && TryParseRange (initial, out int low, out int high))
        {
            rangeView.LowValue = low;
            rangeView.HighValue = high;
        }

        RunnableWrapper<RangeView, (int Low, int High)?> wrapper = new (rangeView)
        {
            Title = options.Title ?? "Select a range (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = rv => rv.RangeResult,
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

        (int Low, int High)? result = wrapper.Result;

        if (result is not { } r)
        {
            return new () { Status = CletRunStatus.Ok, Value = null };
        }

        JsonObject obj = new ()
        {
            ["low"] = r.Low,
            ["high"] = r.High,
        };

        return new () { Status = CletRunStatus.Ok, Value = obj };
    }

    private static bool TryParseRange (string input, out int low, out int high)
    {
        low = 0;
        high = 0;
        int separatorIdx = input.IndexOf ("..", StringComparison.Ordinal);

        if (separatorIdx < 0)
        {
            return false;
        }

        return int.TryParse (input.AsSpan (0, separatorIdx), CultureInfo.InvariantCulture, out low)
               && int.TryParse (input.AsSpan (separatorIdx + 2), CultureInfo.InvariantCulture, out high);
    }
}
