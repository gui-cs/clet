using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class MultiSelectClet : IClet<JsonArray?>
{
    public string PrimaryAlias => "multi-select";
    public IReadOnlyList<string> Aliases => ["multi-select"];
    public string Description => "Presents a list of options with checkboxes and returns the selected texts.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (JsonArray);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("options", "o", typeof (string), "Comma-separated list of options to display.", true, null),
    ];

    public async Task<CletRunResult<JsonArray?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        string[] labels = options.Arguments is { Count: > 0 }
            ? LabelParser.Split (options.Arguments)
            : options.CletOptions?.TryGetValue ("options", out string? optionsValue) == true
                ? LabelParser.Split (optionsValue)
                : [];

        int[] values = new int [labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            values [i] = 1 << i;
        }

        FlagSelector flagSelector = new ()
        {
            Labels = labels,
            Values = values,
        };

        if (initial is not null)
        {
            string[] initialLabels = initial.Split (',');
            int flags = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                if (Array.Exists (initialLabels, l => string.Equals (l.Trim (), labels [i], StringComparison.OrdinalIgnoreCase)))
                {
                    flags |= 1 << i;
                }
            }

            flagSelector.Value = flags;
        }

        RunnableWrapper<FlagSelector, int?> wrapper = new (flagSelector)
        {
            Title = options.Title ?? "Select one or more options (Enter to accept, Esc to cancel)",
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

        int? resultFlags = wrapper.Result;
        JsonArray selected = new ();

        if (resultFlags is { } bits)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    selected.Add (JsonValue.Create (labels [i]));
                }
            }
        }

        return new () { Status = CletRunStatus.Ok, Value = selected };
    }
}
