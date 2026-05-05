using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TgAttribute = Terminal.Gui.Drawing.Attribute;

namespace Clet;

internal sealed class AttributePickerClet : IClet<JsonObject?>
{
    public string PrimaryAlias => "attribute-picker";
    public IReadOnlyList<string> Aliases => ["attribute-picker"];
    public string Description => "Prompts for text attributes (foreground, background, style) and returns a JSON object.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (JsonObject);

    public IReadOnlyList<CletOptionDescriptor> Options => [];

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

        AttributePicker picker = new ();

        foreach (View sub in picker.SubViews)
        {
            if (sub is ColorPicker cp)
            {
                cp.Style.ShowColorName = true;
                cp.ApplyStyleChanges ();
            }
        }

        RunnableWrapper<AttributePicker, TgAttribute?> wrapper = new (picker)
        {
            Title = options.Title ?? "Pick text attributes (Enter to accept, Esc to cancel)",
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

        TgAttribute? result = wrapper.Result;

        if (result is not { } attr)
        {
            return new () { Status = CletRunStatus.Ok, Value = null };
        }

        Color fg = attr.Foreground;
        Color bg = attr.Background;

        JsonObject obj = new ()
        {
            ["fg"] = $"#{fg.R:x2}{fg.G:x2}{fg.B:x2}",
            ["bg"] = $"#{bg.R:x2}{bg.G:x2}{bg.B:x2}",
            ["style"] = attr.Style.ToString ().ToLowerInvariant (),
        };

        return new () { Status = CletRunStatus.Ok, Value = obj };
    }
}
