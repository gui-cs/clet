using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class IntClet : IClet<int?>
{
    public string PrimaryAlias => "int";
    public IReadOnlyList<string> Aliases => ["int"];
    public string Description => "Prompts for an integer value using a numeric spinner.";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (int);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("step", null, typeof (int), "Step increment.", false, "1"),
    ];

    public bool TryValidateInitial (string initial, CletRunOptions options)
        => int.TryParse (initial, CultureInfo.InvariantCulture, out _);

    public async Task<CletRunResult<int?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        NumericUpDown<int> spinner = new ();

        if (options.CletOptions?.TryGetValue ("step", out string? stepStr) == true
            && int.TryParse (stepStr, CultureInfo.InvariantCulture, out int step))
        {
            spinner.Increment = step;
        }

        if (initial is not null
            && int.TryParse (initial, CultureInfo.InvariantCulture, out int initialValue))
        {
            spinner.Value = initialValue;
        }

        RunnableWrapper<NumericUpDown<int>, int?> wrapper = new (spinner)
        {
            Title = options.Title ?? "Enter a number (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = s => s.Value,
            SchemeName = CletStyling.BaseSchemeName,
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
