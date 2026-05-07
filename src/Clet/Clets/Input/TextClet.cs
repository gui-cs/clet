using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
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
        TextField textField = new ()
        {
            Text = initial ?? string.Empty,
            Width = Dim.Fill (),
        };

        RunnableWrapper<TextField, string?> wrapper = new (textField)
        {
            ResultExtractor = t => t.Text,
        };

        return await InputCletRunner.RunAsync (
            app, wrapper, options,
            "Enter text (Enter to accept, Esc to cancel)",
            cancellationToken);
    }
}
