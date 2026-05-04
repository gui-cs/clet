using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class SelectClet : IClet<int?>
{
    public string PrimaryAlias => "select";
    public IReadOnlyList<string> Aliases => ["select"];
    public string Description => "Presents a list of options and returns the zero-based index of the selected item.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (int?);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("options", "o", typeof (string), "Comma-separated list of options to display.", true, null),
    ];

    public Task<CletRunResult<int?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult (new CletRunResult<int?> { Status = CletRunStatus.Cancelled });
        }

        string[] labels = options.CletOptions?.TryGetValue ("options", out string? optionsValue) == true
            ? optionsValue?.Split (',') ?? []
            : [];

        OptionSelector selector = new ()
        {
            Labels = labels,
            AssignHotKeys = true,
        };

        if (int.TryParse (initial, out int initialIndex) && initialIndex >= 0 && initialIndex < labels.Length)
        {
            selector.Value = initialIndex;
        }

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector)
        {
            Title = options.Title ?? "Select an option (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
        };

        app.Run (wrapper);

        return Task.FromResult (cancellationToken.IsCancellationRequested
            ? new CletRunResult<int?> { Status = CletRunStatus.Cancelled }
            : new CletRunResult<int?> { Status = CletRunStatus.Ok, Value = wrapper.Result });
    }
}
