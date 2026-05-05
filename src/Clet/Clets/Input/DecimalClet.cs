using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DecimalClet : IClet<decimal?>
{
    public string PrimaryAlias => "decimal";
    public IReadOnlyList<string> Aliases => ["decimal"];
    public string Description => "Prompts for a decimal value using a numeric spinner.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (decimal);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("step", null, typeof (decimal), "Step increment.", false, "0.1"),
    ];

    public async Task<CletRunResult<decimal?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        NumericUpDown<decimal> spinner = new ()
        {
            Increment = 0.1m,
            Format = "{0:0.###}",
        };

        if (options.CletOptions?.TryGetValue ("step", out string? stepStr) == true
            && decimal.TryParse (stepStr, CultureInfo.InvariantCulture, out decimal step))
        {
            spinner.Increment = step;
        }

        if (initial is not null
            && decimal.TryParse (initial, CultureInfo.InvariantCulture, out decimal initialValue))
        {
            spinner.Value = initialValue;
        }

        RunnableWrapper<NumericUpDown<decimal>, decimal?> wrapper = new (spinner)
        {
            Title = options.Title ?? "Enter a decimal (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = s => s.Value,
        };
        wrapper.Border.Thickness = new Thickness (0, 1, 0, 0);
        wrapper.KeyBindings.Add (Key.Enter, Command.Accept);

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

        return new () { Status = CletRunStatus.Ok, Value = wrapper.Result };
    }
}
