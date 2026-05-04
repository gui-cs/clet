using Terminal.Gui.App;

namespace Clet;

internal interface IViewerClet : IClet
{
    Task<CletRunResult> RunAsync (
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken);
}
