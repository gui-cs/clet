namespace Clet;

internal readonly record struct CletRunResult
{
    public CletRunStatus Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal readonly record struct CletRunResult<T>
{
    public CletRunStatus Status { get; init; }
    public T? Value { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public CletRunResult ToUntyped () => new () { Status = Status, ErrorCode = ErrorCode, ErrorMessage = ErrorMessage };
}
