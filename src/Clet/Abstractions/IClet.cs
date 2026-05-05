using Terminal.Gui.App;

namespace Clet;

internal interface IClet
{
    string PrimaryAlias { get; }
    IReadOnlyList<string> Aliases { get; }
    string Description { get; }
    CletKind Kind { get; }
    Type ResultType { get; }
    IReadOnlyList<CletOptionDescriptor> Options { get; }

    Task<BoxedCletResult> RunBoxedAsync (
        IApplication app,
        string? input,
        CletRunOptions options,
        CancellationToken cancellationToken);
}

internal interface IClet<TValue> : IClet
{
    Task<CletRunResult<TValue>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken);

    async Task<BoxedCletResult> IClet.RunBoxedAsync (
        IApplication app,
        string? input,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        CletRunResult<TValue> result = await RunAsync (app, input, options, cancellationToken);

        return new (result.Status, result.Value, result.ErrorCode, result.ErrorMessage);
    }
}
