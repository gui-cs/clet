using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class TextClet : IClet<string?>
{
    public string PrimaryAlias => "text";
    public IReadOnlyList<string> Aliases => ["text", "multiline-text", "mt"];
    public string Description => "Prompts for multi-line text input using an editor and returns the entered string.";
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

        int rows = options.Rows ?? 5;

        Editor editor = new ()
        {
            Document = new TextDocument (initial ?? string.Empty),
            Width = Dim.Fill (),
            Height = rows,
            ConvertTabsToSpaces = true,
        };

        Button okButton = new ()
        {
            Text = "_OK",
            Y = Pos.Bottom (editor),
        };

        RunnableWrapper<Editor, string?> wrapper = new (editor)
        {
            Title = options.Title ?? "Enter text (OK to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = e => e.Document?.Text,
            SchemeName = CletStyling.BaseSchemeName,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);
        wrapper.Add (okButton);

        okButton.Accepted += (_, _) => wrapper.InvokeCommand (Command.Accept);

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
