namespace Clet;

internal sealed record CletRunOptions
{
    public string? Title { get; init; }
    public bool JsonOutput { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool Fullscreen { get; init; }
    public bool Cat { get; init; }
    public string? OutputPath { get; init; }
    public int? Rows { get; init; }
    public IReadOnlyDictionary<string, string>? CletOptions { get; init; }
    public IReadOnlyList<string>? Arguments { get; init; }
}
