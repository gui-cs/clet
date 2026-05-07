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

    /// <summary>Paths explicitly allowed for file reading (bypasses extension + cwd checks).</summary>
    public IReadOnlyList<string>? AllowedFiles { get; init; }

    /// <summary>When true, binary file content (NUL bytes) is permitted.</summary>
    public bool AllowBinary { get; init; }

    /// <summary>When true, disables browser-mode navigation (back/forward, top bar) for viewer clets.</summary>
    public bool NoBrowse { get; init; }
}
