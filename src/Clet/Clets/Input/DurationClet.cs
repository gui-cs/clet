using System.Globalization;
using System.Xml;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class DurationClet : IClet<string?>
{
    public string PrimaryAlias => "duration";
    public IReadOnlyList<string> Aliases => ["duration"];
    public string Description => "Prompts for a duration and returns an ISO-8601 duration string (e.g. PT1H30M).";
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

        if (initial is not null)
        {
            bool initialParsed = false;

            try
            {
                TimeSpan parsed = XmlConvert.ToTimeSpan (initial);
                editor.Value = parsed;
                initialParsed = true;
            }
            catch (FormatException)
            {
            }

            if (!initialParsed)
            {
                if (TimeSpan.TryParse (initial, CultureInfo.InvariantCulture, out TimeSpan fallback))
                {
                    editor.Value = fallback;
                }
                else
                {
                    return new () { Status = CletRunStatus.Error, ErrorCode = "usage", ErrorMessage = $"invalid --initial value '{initial}' for duration. Expected an ISO-8601 duration (e.g. PT1H30M) or timespan (e.g. 1:30:00)." };
                }
            }
        }

        RunnableWrapper<TimeEditor, TimeSpan?> wrapper = new (editor)
        {
            Title = options.Title ?? "Enter a duration (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
            ResultExtractor = e => ((IValue<TimeSpan>)e).Value,
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

        TimeSpan? result = wrapper.Result;
        string? formatted = result is { } ts ? XmlConvert.ToString (ts) : null;

        return new () { Status = CletRunStatus.Ok, Value = formatted };
    }
}
