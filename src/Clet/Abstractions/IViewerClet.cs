using Terminal.Gui.App;

namespace Clet;

internal interface IViewerClet : IClet
{
    Task<CletRunResult> RunAsync (
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken);

    async Task<BoxedCletResult> IClet.RunBoxedAsync (
        IApplication app,
        string? input,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        CletRunResult result = await RunAsync (app, input, options, cancellationToken);

        return new (result.Status, null, result.ErrorCode, result.ErrorMessage);
    }
}
