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
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

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

        string title = options.CletOptions?.TryGetValue ("prompt", out string? promptValue) == true && promptValue is not null
            ? promptValue
            : options.Title ?? "Confirm (Enter to accept, Esc to cancel)";

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector)
        {
            Title = title,
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
        bool? result = selectedIndex switch
        {
            0 => true,
            1 => false,
            _ => null,
        };

        return new () { Status = CletRunStatus.Ok, Value = result };
    }
}
