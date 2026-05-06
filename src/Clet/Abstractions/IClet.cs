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

    /// <summary>
    /// Whether this clet consumes positional arguments. Defaults to <see langword="false"/>.
    /// Clets that accept positional args (e.g. <c>select</c>, <c>multi-select</c>, <c>md</c>)
    /// should override this to return <see langword="true"/>.
    /// </summary>
    bool AcceptsPositionalArgs => false;

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
