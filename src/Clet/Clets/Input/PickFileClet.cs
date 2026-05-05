using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class PickFileClet : IClet<JsonNode?>
{
    public string PrimaryAlias => "pick-file";
    public IReadOnlyList<string> Aliases => ["pick-file"];
    public string Description => "Opens a file picker dialog and returns the selected file path(s).";
    public CletKind Kind => CletKind.Input;
    public Type ResultType => typeof (JsonNode);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("multi", "m", typeof (bool), "Allow selecting multiple files.", false, "false"),
        new ("root", "r", typeof (string), "Starting directory.", false, null),
        new ("filter", "f", typeof (string), "File type filter (e.g. \"*.cs\").", false, null),
    ];

    public async Task<CletRunResult<JsonNode?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        bool multi = options.CletOptions?.TryGetValue ("multi", out string? multiStr) == true
                     && string.Equals (multiStr, "true", StringComparison.OrdinalIgnoreCase);

        string? root = options.CletOptions?.TryGetValue ("root", out string? rootStr) == true ? rootStr : null;

        OpenDialog dialog = new ()
        {
            Title = options.Title ?? "Select a file (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            Height = 25,
            AllowsMultipleSelection = multi,
        };

        if (root is not null)
        {
            dialog.Path = root;
        }

        try
        {
            await app.RunAsync (dialog, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        IReadOnlyList<string>? paths = dialog.FilePaths;

        if (paths is null || paths.Count == 0)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        if (multi)
        {
            List<string> sorted = new (paths);
            sorted.Sort (StringComparer.Ordinal);
            JsonArray arr = new ();

            foreach (string p in sorted)
            {
                arr.Add (JsonValue.Create (p));
            }

            return new () { Status = CletRunStatus.Ok, Value = arr };
        }

        return new () { Status = CletRunStatus.Ok, Value = JsonValue.Create (paths [0]) };
    }
}
