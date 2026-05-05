using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class SelectClet : IClet<string?>
{
    public string PrimaryAlias => "select";
    public IReadOnlyList<string> Aliases => ["select"];
    public string Description => "Presents a list of options and returns the text of the selected item.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("options", "o", typeof (string), "Comma-separated list of options to display.", true, null),
    ];

    public async Task<CletRunResult<string?>> RunAsync (
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

        OptionSelector selector = new ()
        {
            Labels = labels,
            AssignHotKeys = true,
        };

        if (initial is not null)
        {
            int initialIdx = Array.FindIndex (labels, l => string.Equals (l, initial, StringComparison.OrdinalIgnoreCase));

            if (initialIdx >= 0)
            {
                selector.Value = initialIdx;
            }
        }

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector)
        {
            Title = options.Title ?? "Select an option (Enter to accept, Esc to cancel)",
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

        int? selectedIndex = wrapper.Result;
        string? selectedText = selectedIndex is >= 0 and var idx && idx < labels.Length
            ? labels [idx]
            : null;

        return new () { Status = CletRunStatus.Ok, Value = selectedText };
    }
}
