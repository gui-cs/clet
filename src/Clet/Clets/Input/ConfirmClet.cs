using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class ConfirmClet : IClet<bool?>
{
    public string PrimaryAlias => "confirm";
    public IReadOnlyList<string> Aliases => ["confirm"];
    public string Description => "Prompts for a yes/no confirmation and returns a boolean.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (bool);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("prompt", "p", typeof (string), "Custom prompt text displayed as the title.", false, null),
    ];

    public bool TryValidateInitial (string initial, CletRunOptions options)
        => string.Equals (initial, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "false", StringComparison.OrdinalIgnoreCase)
           || string.Equals (initial, "no", StringComparison.OrdinalIgnoreCase);

    public async Task<CletRunResult<bool?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        OptionSelector selector = new ()
        {
            Labels = ["Yes", "No"],
            AssignHotKeys = true,
        };

        if (initial is not null)
        {
            if (string.Equals (initial, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals (initial, "yes", StringComparison.OrdinalIgnoreCase))
            {
                selector.Value = 0;
            }
            else if (string.Equals (initial, "false", StringComparison.OrdinalIgnoreCase)
                     || string.Equals (initial, "no", StringComparison.OrdinalIgnoreCase))
            {
                selector.Value = 1;
            }
        }

        // --prompt option overrides --title for the window title
        string effectiveTitle = options.CletOptions?.TryGetValue ("prompt", out string? promptValue) == true && promptValue is not null
            ? promptValue
            : "Confirm (Enter to accept, Esc to cancel)";

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector);

        return await InputCletRunner.RunAsync<OptionSelector, int?, bool?> (
            app, wrapper, options,
            effectiveTitle,
            cancellationToken,
            result =>
            {
                bool? value = result switch
                {
                    0 => true,
                    1 => false,
                    _ => null,
                };

                return new () { Status = CletRunStatus.Ok, Value = value };
            },
            addEnterBinding: false);
    }
}
