using System.Globalization;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class TimeClet : IClet<string?>
{
    public string PrimaryAlias => "time";
    public IReadOnlyList<string> Aliases => ["time"];
    public string Description => "Prompts for a time and returns an ISO-8601 time string (HH:MM:SS).";
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

        TimeEditor editor = new ();

        if (initial is not null
            && TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out TimeSpan initialTime))
        {
            editor.Value = initialTime;
        }

        RunnableWrapper<TimeEditor, TimeSpan?> wrapper = new (editor)
        {
            Title = options.Title ?? "Select a time (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = e => ((IValue<TimeSpan>)e).Value,
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

        TimeSpan? result = wrapper.Result;
        string? formatted = result?.ToString (@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        return new () { Status = CletRunStatus.Ok, Value = formatted };
    }
}
