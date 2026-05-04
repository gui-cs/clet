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
}

internal interface IClet<TValue> : IClet
{
    Task<CletRunResult<TValue>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken);
}
