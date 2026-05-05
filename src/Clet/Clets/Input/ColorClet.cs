using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class ColorClet : IClet<string?>
{
    public string PrimaryAlias => "color";
    public IReadOnlyList<string> Aliases => ["color"];
    public string Description => "Prompts for a color and returns a hex string (#rrggbb).";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (string);

    public IReadOnlyList<CletOptionDescriptor> Options => [];

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

        ColorPicker picker = new ();
        picker.Style.ShowColorName = true;
        picker.ApplyStyleChanges ();

        if (initial is not null && Color.TryParse (initial, null, out Color parsed))
        {
            picker.SelectedColor = parsed;
        }

        RunnableWrapper<ColorPicker, Color?> wrapper = new (picker)
        {
            Title = options.Title ?? "Pick a color (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
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

        Color? result = wrapper.Result;
        string? hex = result is { } c ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : null;

        return new () { Status = CletRunStatus.Ok, Value = hex };
    }
}
