using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DateClet : IClet<string?>
{
    public string PrimaryAlias => "date";
    public IReadOnlyList<string> Aliases => ["date"];
    public string Description => "Prompts for a date and returns an ISO-8601 date string (YYYY-MM-DD).";
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

        DatePicker picker = new ();

        if (initial is not null
            && DateTime.TryParse (initial, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime initialDate))
        {
            picker.Value = initialDate;
        }

        RunnableWrapper<DatePicker, DateTime?> wrapper = new (picker)
        {
            Title = options.Title ?? "Select a date (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = p => p.Value,
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

        DateTime? result = wrapper.Result;
        string? formatted = result?.ToString ("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new () { Status = CletRunStatus.Ok, Value = formatted };
    }
}
