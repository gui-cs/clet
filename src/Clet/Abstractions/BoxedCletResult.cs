namespace Clet;

internal readonly record struct BoxedCletResult (
    CletRunStatus Status,
    object? Value,
    string? ErrorCode,
    string? ErrorMessage);
