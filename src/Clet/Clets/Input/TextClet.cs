using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class TextClet : IClet<string?>
{
    public string PrimaryAlias => "text";
    public IReadOnlyList<string> Aliases => ["text"];
    public string Description => "Prompts for free-form text input and returns the entered string.";
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

        TextField textField = new ()
        {
            Text = initial ?? string.Empty,
        };

        RunnableWrapper<TextField, string?> wrapper = new (textField)
        {
            Title = options.Title ?? "Enter text (Enter to accept, Esc to cancel)",
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

        string? result = wrapper.Result;

        return new () { Status = CletRunStatus.Ok, Value = result };
    }
}
