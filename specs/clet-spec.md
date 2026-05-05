# `clet` Implementation Spec

**Status:** draft v0.4 · for review · companion to the PR/FAQ in [issue #5155](https://github.com/gui-cs/Terminal.Gui/issues/5155)

This is the implementation spec. It assumes the PR/FAQ is broadly accepted and covers what to build, where it lives, what changes in Terminal.Gui to support it, how it ships, and how it's tested.

---

## 1. Scope and Non-Goals

### In scope (v1.0)

- New repo `gui-cs/clet` containing all clet code: abstractions, registry, JSON, source generator, built-in clets, CLI binary, native installer manifests, release automation.
- Targeted changes to `gui-cs/Terminal.Gui` core (§3) that benefit TG generally and unblock clet specifically.
- Fourteen input clets and one viewer clet (`md`) statically registered in v1.0.
- Native installer channels: Homebrew (gui-cs tap), WinGet, .NET tool. NativeAOT for native channels.
- Auto-release workflow tied to TG releases. Version 1:1 with TG.
- JSON output contract (schemaVersion 1).
- Inline input rendering; alt-screen viewer rendering.
- Theming via TG's `ConfigurationManager`.

### Out of scope (deferred to v2 or later)

- Extracting clet abstractions into a published NuGet for third-party consumption (`Clet.Abstractions`). Today, `IClet` is internal-to-the-binary; v2 may publish it.
- Third-party clet runtime loading (`Assembly.LoadFrom` into the AOT'd CLI).
- `password` clet.
- Additional viewer clets (`json`, `log`, `diff`).
- Telemetry beyond a one-shot opt-in install ping.
- Embedded/inline-in-other-TG-app use of clets.

---

## 2. Architecture Overview

Two repos. One assembly that matters (the CLI exe). One release cadence.

```
gui-cs/Terminal.Gui                           gui-cs/clet
├── Terminal.Gui/                             ├── src/
│     (core; §3 tweaks land here,             │   ├── Clet/
│      no clet-specific types)                │   │     Abstractions/  (IClet, ICletRegistry, ...)
├── Tests/                                    │   │     Registry/
│     (TG core tests only;                    │   │     Json/          (CletJsonContext, schema)
│      clet tests live in gui-cs/clet)        │   │     Clets/Input/   (Text, PickFile, ...)
└── .github/workflows/                        │   │     Clets/Viewer/  (Markdown)
      notify-clet-on-release.yml (NEW)        │   │     Hosting/       (Program.cs, CLI)
                                              │   └── Clet.SourceGen/  (build-time analyzer)
                                              ├── tests/
                                              │     Clet.UnitTests/
                                              │     Clet.IntegrationTests/
                                              │     Clet.SmokeTests/
                                              │     Clet.ContractTests/
                                              │     Clet.PerfTests/
                                              ├── packaging/
                                              │     homebrew/clet.rb.template
                                              │     winget/manifests.template
                                              │     dotnet-tool/Clet.Tool.csproj
                                              ├── .github/workflows/
                                              │     ci.yml
                                              │     release-on-tg-release.yml
                                              │     publish-homebrew.yml
                                              │     publish-winget.yml
                                              │     publish-nuget.yml
                                              └── scripts/
                                                    sign-macos.sh
                                                    sign-windows.ps1
                                                    notarize-macos.sh
                                                    update-homebrew-tap.sh
```

**Process model.** The `clet` binary is a thin shell:
1. Parses CLI args (System.CommandLine).
2. Looks up the alias in its in-process `ICletRegistry`.
3. Initializes a Terminal.Gui `IApplication`.
4. Calls `clet.RunAsync(...)`.
5. Serializes the result, emits to stdout, exits with the right code.

All of (3)+(4) is plain Terminal.Gui hosting against TG's public API. The clet itself is a Terminal.Gui View. Nothing in TG core knows about clets; nothing in clets requires private TG API.

---

## 3. Terminal.Gui Changes Required

Most of what an early draft of this spec assumed would need to change in TG is already done or already tracked, including:

- **Inline rendering** is shipping today and exercised by `md`, the inline examples, and `gui-cs/ai`.
- **AOT compatibility** is tracked in TG core; remaining issues surface most efficiently by building `clet` and running it. The §6.6 publish tests are the discovery mechanism.
- **`ConfigurationManager`** path-based loading is broadly used and tested.
- **`Markdown` View** is vetted for the read-only, dismissable, themed shape clet needs.
- **Terminal-driver inline-capable detection** is already in place.

The two items previously outstanding ([#5157](https://github.com/gui-cs/Terminal.Gui/issues/5157) and [#5158](https://github.com/gui-cs/Terminal.Gui/issues/5158)) have **landed on TG `develop`**. clet builds against the TG version named by the dispatch payload via `<TerminalGuiVersion>` in `src/Clet/Clet.csproj`; the local-development default tracks a known-good develop build. There is no longer a "pin to remove" — see [D-020](decisions.md#d-020) for the continuous-release design.

### 3.1 Cancellation token plumbing (LANDED on `develop`) ; was [#5157](https://github.com/gui-cs/Terminal.Gui/issues/5157)

`IApplication.RunAsync(Toplevel, CancellationToken)` is on TG `develop`. Clet binds to it directly; no fallback path needed.

Clet's wire contract does not depend on TG's decision about whether `IValue<T>.Value` is readable after cancel; the cancel envelope is fixed at `{"schemaVersion":1,"status":"cancelled"}` regardless (§4.3). Any partial-result behavior TG settles on is welcome but not load-bearing for clet.

### 3.2 FileDialog: typed result refactor (LANDED on `develop`) ; was [#5158](https://github.com/gui-cs/Terminal.Gui/issues/5158)

`FileDialog` now inherits from `Dialog<IReadOnlyList<string>?>`. A non-null list represents accept; `null` represents cancel. Read-only is the right shape for a typed result — callers do not mutate the dialog's selection after Run returns.

The `pick-file` and `pick-directory` clets bind to `IValue<IReadOnlyList<string>?>` directly, with no per-clet glue code. The §4.3.2 per-clet `value` shape table already specifies the resulting JSON wire format (string for single-select, array of strings for `--multi`), independent of TG's collection type.

This change is breaking for any TG v2 caller that was reading the old `int` OK/Cancel sentinel; that breakage was accepted by the TG team because v2.1 already carries one breaking change.

---

## 4. `gui-cs/clet` Repo

This repo holds everything: abstractions, registry, JSON, source generator, built-in clets, the CLI binary, packaging, and release automation. One assembly is published; everything else is build-time only or test-only.

### 4.1 Project layout

```
gui-cs/clet/
├── README.md
├── LICENSE
├── Clet.sln
├── src/
│   ├── Clet/                              (single Exe project; PublishAot=true; net10.0)
│   │   ├── Clet.csproj
│   │   ├── Abstractions/
│   │   │     IClet.cs
│   │   │     IViewerClet.cs
│   │   │     ICletRegistry.cs
│   │   │     CletKind.cs                  (Input | Viewer)
│   │   │     CletRunOptions.cs
│   │   │     CletRunResult.cs             (record + record<T>)
│   │   │     CletRunStatus.cs             (Ok | Cancelled | Error | NoResult)
│   │   │     CletOptionDescriptor.cs
│   │   ├── Registry/
│   │   │     CletRegistry.cs
│   │   │     CletRegistration.cs
│   │   ├── Json/
│   │   │     CletJsonContext.cs           ([JsonSerializable] for source-gen)
│   │   │     CletJsonOutput.cs
│   │   │     SchemaV1.cs
│   │   ├── Clets/
│   │   │   ├── Input/
│   │   │   │     TextClet.cs
│   │   │   │     IntClet.cs
│   │   │   │     DecimalClet.cs
│   │   │   │     SelectClet.cs
│   │   │   │     MultiSelectClet.cs
│   │   │   │     ConfirmClet.cs
│   │   │   │     PickFileClet.cs
│   │   │   │     PickDirectoryClet.cs
│   │   │   │     DateClet.cs
│   │   │   │     TimeClet.cs
│   │   │   │     DurationClet.cs
│   │   │   │     ColorClet.cs
│   │   │   │     AttributePickerClet.cs
│   │   │   │     RangeClet.cs
│   │   │   └── Viewer/
│   │   │         MarkdownClet.cs
│   │   └── Hosting/
│   │         Program.cs                   (Main, async)
│   │         CommandLineRoot.cs           (System.CommandLine root)
│   │         AliasDispatcher.cs
│   │         OutputFormatter.cs
│   │         ExitCodes.cs
│   └── Clet.SourceGen/                    (build-time analyzer; not shipped)
│         Clet.SourceGen.csproj
│         CletAttribute.cs
│         RegistrationGenerator.cs
├── tests/
│   ├── Clet.UnitTests/                    (parallelizable; pure logic)
│   ├── Clet.IntegrationTests/             (TG hosting + run-loop)
│   ├── Clet.SmokeTests/                   (process-level: spawn the exe)
│   ├── Clet.ContractTests/                (JSON schema validation)
│   └── Clet.PerfTests/                    (cold-start budgets)
├── packaging/
│   ├── homebrew/
│   │   └── clet.rb.template
│   ├── winget/
│   │   ├── gui-cs.clet.installer.yaml.template
│   │   ├── gui-cs.clet.locale.en-US.yaml.template
│   │   └── gui-cs.clet.yaml.template
│   └── dotnet-tool/
│       └── Clet.Tool.csproj
├── .github/
│   └── workflows/
│       ├── ci.yml                          (build, test on push)
│       ├── release-on-tg-release.yml       (triggered by TG release)
│       ├── publish-homebrew.yml
│       ├── publish-winget.yml
│       └── publish-nuget.yml
├── scripts/
│   ├── sign-macos.sh
│   ├── sign-windows.ps1
│   ├── notarize-macos.sh
│   └── update-homebrew-tap.sh
└── docs/
    ├── installing.md
    ├── json-schema.md
    └── exit-codes.md
```

**One src project (`Clet`).** Abstractions, registry, JSON, built-in clets, and `Program.Main` all compile into one assembly. No internal NuGet packaging. The source generator is a separate project because Roslyn analyzers must be (build-time only, never referenced at runtime).

### 4.2 Public types (sketch)

These are `internal` to the `Clet` assembly in v1.0. v2 may extract them to `Clet.Abstractions` and publish, when third-party plugin loading lands.

```csharp
namespace Clet;

internal enum CletKind { Input, Viewer }

internal enum CletRunStatus { Ok, Cancelled, Error, NoResult }

internal sealed record CletRunOptions
{
    public string? Title { get; init; }
    public bool JsonOutput { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool Fullscreen { get; init; }
    public IReadOnlyDictionary<string, string>? CletOptions { get; init; }
}

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

internal interface IViewerClet : IClet
{
    Task<CletRunResult> RunAsync (
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken);
}

internal interface ICletRegistry
{
    void Register (IClet clet);
    bool TryResolve (string alias, out IClet clet);
    IReadOnlyCollection<IClet> All { get; }
}

internal sealed record CletOptionDescriptor (
    string Name,
    string? ShortName,
    Type ValueType,
    string Description,
    bool Required,
    string? DefaultValue);
```

### 4.3 JSON schema (v1)

```json
{
  "$id": "https://gui-cs.github.io/clet/schema/v1.json",
  "type": "object",
  "required": ["schemaVersion", "status"],
  "properties": {
    "schemaVersion": { "const": 1 },
    "status": { "enum": ["ok", "cancelled", "error", "no-result"] },
    "value":   { },
    "code":    { "type": "string" },
    "message": { "type": "string" }
  },
  "allOf": [
    { "if": { "properties": { "status": { "const": "ok"    } } },
      "then": { "anyOf": [ { "required": ["value"] }, { "not": { "required": ["value"] } } ] } },
    { "if": { "properties": { "status": { "const": "error" } } },
      "then": { "required": ["code", "message"] } }
  ]
}
```

**Normative envelopes:**
```json
{ "schemaVersion": 1, "status": "ok", "value": <T> }                  // input clet success
{ "schemaVersion": 1, "status": "ok" }                                // viewer clet dismiss
{ "schemaVersion": 1, "status": "cancelled" }                         // cancel (any clet)
{ "schemaVersion": 1, "status": "error", "code": "...", "message": "..." }
{ "schemaVersion": 1, "status": "no-result" }
```

**No `type` field on the envelope.** The result type is advertised once, per-alias, in `clet list --json`. Agents that need it cache the registry; they do not branch on a per-call CLR type name. This keeps the wire format language-agnostic (no `System.String`-shaped values leak across the boundary).

**Cancel is decoupled from TG.** On cancel, clet emits `{"schemaVersion":1,"status":"cancelled"}` and nothing else — no `value`, no `code`, no partial result — regardless of whether `IValue<T>.Value` is readable on the underlying View at the moment of cancellation. This is the contract clet promises to AI-agent consumers; TG's eventual answer to #5157's "disposition of `IValue<T>.Value` on cancellation" question is a TG-internal concern.

Schema is published in this repo under `docs/json-schema.md` and pinned in `Json/SchemaV1.cs`. Contract tests (§6.4) validate every emitted line against this schema.

### 4.3.1 Schema versioning policy

`schemaVersion: 1` is the contract for the entire `clet 1.x` line. Changes within `1.x` are additive only — existing fields never change meaning, never become required, never change type. A `schemaVersion: 2` is permitted only at a `clet 2.0.0` boundary, and `clet 2.x` must accept `--schema-version 1` to emit the v1 envelope for at least one minor release of the 2.x line, giving consumers a parallel-period to migrate. Breakage notices land in `gui-cs/clet` release notes and in the footer of `clet --version` for the parallel period.

### 4.3.2 Per-clet `value` shapes

For schema-lock at v0.5, the shape of `value` is fixed per alias. Most clets emit a scalar; the table below names the non-scalar cases explicitly so consumers can encode them once.

| Alias                         | `value` shape                                                |
|-------------------------------|--------------------------------------------------------------|
| `text`                        | string                                                       |
| `int`                         | integer                                                      |
| `decimal`                     | number (JSON number; consumer decides float vs decimal)      |
| `confirm`                     | boolean                                                      |
| `select`                      | string (label text of the selected item — see [D-008](decisions.md)) |
| `multi-select`                | array of strings (label texts, in display order — see [D-009](decisions.md)) |
| `pick-file`                   | string (path)                                                |
| `pick-file --multi`           | array of strings (paths, ascending sort)                     |
| `pick-directory`              | string (path)                                                |
| `date`                        | string, ISO-8601 date (`YYYY-MM-DD`)                         |
| `time`                        | string, ISO-8601 time (`HH:MM:SS`)                           |
| `duration`                    | string, ISO-8601 duration (`PT1H30M`)                        |
| `color`                       | string, `#RRGGBB` (lowercase hex)                            |
| `attribute-picker`            | object, `{"fg": "#RRGGBB", "bg": "#RRGGBB", "style": "..."}` |
| `range`                       | object, `{"low": <T>, "high": <T>}` (`<T>` = scalar of type; `int` only at v0.3 — see [D-011](decisions.md)) |

### 4.4 Source-generated registration

For AOT compatibility and to avoid `typeof(...)` registration spam in `Program.Main`:

```csharp
[Clet ("text", typeof (string))]
internal sealed class TextClet : IClet<string> { ... }
```

The generator produces `BuiltInClets.RegisterAll(ICletRegistry registry)` which calls `registry.Register(new TextClet())` for each.

### 4.5 Built-in clet implementation pattern

Each input clet wraps an existing TG View in `RunnableWrapper<TView, TResult>`, which auto-extracts the typed result via `IValue<TResult>`. The pattern mirrors `Examples/InlineSelect/Program.cs` in `gui-cs/Terminal.Gui` (the canonical inline-mode example).

Sketch for `SelectClet` (the first clet to build; replicates `InlineSelect`):

```csharp
[Clet ("select", typeof (int?))]
internal sealed class SelectClet : IClet<int?>
{
    public async Task<CletRunResult<int?>> RunAsync (
        IApplication app,
        string? initial,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        string[] labels = options.CletOptions? ["options"]?.Split (',') ?? [];

        OptionSelector selector = new ()
        {
            Labels = labels,
            AssignHotKeys = true,
        };

        RunnableWrapper<OptionSelector, int?> wrapper = new (selector)
        {
            Title = options.Title ?? "Select an option (Enter to accept, Esc to cancel)",
            Width = Dim.Fill (),
            BorderStyle = LineStyle.Rounded,
        };

        await app.RunAsync (wrapper, cancellationToken);

        return cancellationToken.IsCancellationRequested
            ? new () { Status = CletRunStatus.Cancelled }
            : new () { Status = CletRunStatus.Ok, Value = wrapper.Result };
    }
}
```

**Pattern observations:**
- The clet does not own the run loop or the application lifecycle; the host (`Program.Main`) does.
- `RunnableWrapper<TView, TResult>` is the existing TG primitive that bridges Views to typed results. Clet does not invent a new wrapper.
- `await app.RunAsync(wrapper, ct)` is the `IApplication.RunAsync(Toplevel, CancellationToken)` overload that landed on TG `develop` (§3.1); clet binds to it directly.
- Viewer clets follow the same shape but use a Markdown-shaped View (or other read-only View) and return `CletRunResult` (no `T`).

**Initial-value parsing.** When a clet's `TResult` implements `System.IParsable<TResult>` (or `System.ISpanParsable<TResult>`) from .NET 7+, the `--initial <string>` flag parses for free with no reflection:

```csharp
if (TResult.TryParse (initial, CultureInfo.InvariantCulture, out TResult parsed))
{
    selector.Value = parsed;  // or whatever IValue<T> setter the View exposes
}
```

This is the standard .NET parsing contract; we don't invent a new interface. Most relevant types already implement it:
- `int`, `decimal`, `bool`, `DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `IPAddress`: built-in.
- `Color`: TG already implements `ISpanParsable<Color>` (`Terminal.Gui/Drawing/Color/Color.cs`).
- Custom records and enums: trivially implementable (often a one-line `static abstract` override).

For types where `IParsable` doesn't fit (collections like `List<string>`, multi-select results, free-form structured input), the clet registers a parser delegate as part of `[Clet]` registration. AOT-clean either way (static-abstract dispatch is an interface call resolved at link time).

### 4.6 `Program.Main` outline

```csharp
internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        using CancellationTokenSource cts = new ();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel (); };

        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        CommandLineRoot root = new (registry);

        return await root.InvokeAsync (args, cts.Token);
    }
}
```

### 4.7 CLI surface

```
clet <alias> [positional...] [--initial <value>] [--title <text>] [--json] [--timeout 30s] [--fullscreen] [--<opt> <value>]...
clet list [--json]
clet help <alias>
clet --help
clet --version
```

**`--help` banner.** `clet --help` (and `clet` with no arguments) prints the ASCII logo followed by a plain-text usage summary. The logo is the approved three-line box-drawing art:

```
  ╔═╗╦  ╔═╗╔╦╗
  ║  ║  ╠═  ║
  ╚═╝╩═╝╚═╝ ╩
```

Plain-text help renders at v0.11. Markdown-rendered help (Terminal.Gui `Markdown` View) defers to v0.5.

**Built-in flags.** `--initial`, `--title`, `--json`, `--timeout`, and `--fullscreen` are parsed at the host level and apply to every clet. Anything else of the form `--<name> <value>` is forwarded as a clet-specific option (see each clet's `clet help <alias>`). Bare positional tokens are forwarded as `CletRunOptions.Arguments` for clets that consume them (e.g. `select`, `multi-select`); other clets ignore them. See decisions log D-014 for why `--title` is a host flag rather than a per-clet option.

**Defaults.** Input clets render inline. Viewer clets (`md`) render fullscreen. `--fullscreen` forces fullscreen for input clets; it's a no-op for viewers.

**Theming.** No `--theme` flag. Theme selection goes through `ConfigurationManager`'s existing mechanisms (config files, env var, system theme); `clet` honors whatever it resolves.

**Help rendering.** `clet --help` and `clet help <alias>` render Markdown to ANSI escape sequences and write to stdout (print mode), then exit immediately — they do not open an interactive viewer (see D-016). "Same code path" means the same Terminal.Gui `Markdown` rendering engine with `TextMateSyntaxHighlighter`, not the same interactive fullscreen mode. Root help reads from an embedded `src/Clet/Help/overview.md` resource; per-alias help is generated dynamically from `IClet` metadata by `MarkdownHelpRenderer.BuildAliasHelpMarkdown()`. This keeps help pipeable (`clet --help | less`) and consumable by AI agents reading stdout.

### 4.8 Exit code mapping

| Status                | Exit |
|-----------------------|-----:|
| Ok (input or viewer)  |    0 |
| NoResult              |    1 |
| Usage error           |    2 |
| Validation error      |   65 |
| I/O error             |   74 |
| Cancelled             |  130 |

### 4.9 NativeAOT publish settings

In `Clet.csproj`:
```xml
<PublishAot>true</PublishAot>
<InvariantGlobalization>false</InvariantGlobalization>
<StackTraceSupport>true</StackTraceSupport>
<DebuggerSupport>false</DebuggerSupport>
<EventSourceSupport>false</EventSourceSupport>
<UseSystemResourceKeys>true</UseSystemResourceKeys>
```

Target binary size: ~8MB. Cold-start budget: <100ms on Apple Silicon, <150ms on Windows x64.

---

## 5. Release and Update Pipeline

### 5.1 Trigger

`gui-cs/Terminal.Gui` fires a `repository_dispatch` to `gui-cs/clet` on **two** events: every `*-develop.NN` NuGet publish (continuous develop channel) and every release tag (stable channel). The `release-on-tg.yml` workflow consumes both. **Channel is derived from the version string:** if `tg_version` contains `-` (a SemVer prerelease suffix), the dispatch is treated as develop; otherwise it's a release. See [D-020](decisions.md#d-020) for the rationale.

```yaml
# gui-cs/Terminal.Gui/.github/workflows/notify-clet.yml (NEW; only TG-side clet artifact)
on:
  release:
    types: [published]                # stable channel
  workflow_run:
    workflows: ["Publish develop NuGet"]
    types: [completed]                # develop channel
jobs:
  notify-clet:
    runs-on: ubuntu-latest
    if: ${{ github.event_name == 'release' || github.event.workflow_run.conclusion == 'success' }}
    steps:
      - uses: peter-evans/repository-dispatch@v3
        with:
          token: ${{ secrets.CLET_DISPATCH_PAT }}
          repository: gui-cs/clet
          event-type: ${{ github.event_name == 'release' && 'tg-released' || 'tg-develop-published' }}
          client-payload: |
            {"tg_version": "${{ github.event.release.tag_name || github.event.workflow_run.head_commit.message }}"}
```

The exact develop-channel hook into TG's existing publish workflow lives on the TG side; clet's contract is "you fire one of these two dispatches with a `tg_version`, we do the rest."

### 5.2 Build matrix

```yaml
# gui-cs/clet/.github/workflows/release-on-tg.yml
on:
  repository_dispatch:
    types: [tg-released, tg-develop-published]
jobs:
  build:
    strategy:
      matrix:
        rid:
          - osx-arm64
          - osx-x64
          - linux-x64
          - linux-arm64
          - win-x64
          - win-arm64
        include:
          - rid: osx-arm64   ; runner: macos-14
          - rid: osx-x64     ; runner: macos-13
          - rid: linux-x64   ; runner: ubuntu-22.04
          - rid: linux-arm64 ; runner: ubuntu-22.04-arm
          - rid: win-x64     ; runner: windows-2022
          - rid: win-arm64   ; runner: windows-11-arm
    runs-on: ${{ matrix.runner }}
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/Clet -c Release -r ${{ matrix.rid }} --self-contained -p:PublishAot=true
      - name: Smoke test
        run: ./scripts/smoke-test.sh
      - name: Sign (macOS)
        if: startsWith(matrix.rid, 'osx-')
        run: ./scripts/sign-macos.sh
      - name: Sign (Windows)
        if: startsWith(matrix.rid, 'win-')
        run: ./scripts/sign-windows.ps1
      - uses: actions/upload-artifact@v4
```

### 5.3 Smoke test gate (P0; release fails closed)

Before any publish step, every built binary runs a smoke matrix. The gate is process-level: it spawns the AOT'd binary as a real OS process, drives it from outside, and asserts on exit code + stdout JSON.

**Driver:** [`gui-cs/TUIcast`](https://github.com/gui-cs/TUIcast) in deterministic-script mode. TUIcast spawns the target binary inside a PTY (`node-pty`), writes keystrokes directly to the PTY file descriptor (not stdin redirection — Terminal.Gui drivers don't read keystrokes from stdin the way bash heredocs assume), and captures the asciinema stream as a side effect. The deterministic mode takes a comma-separated keystroke script (`"wait:500,ArrowDown,Enter"`) — no AI in the loop, fully reproducible. TUIcast's `poc-uicatalog.yml` workflow already proves this stack works against a Terminal.Gui binary in GitHub Actions.

**Cases:**

1. `clet --version` returns the clet version and the Terminal.Gui version (one per line: `clet <ver>` then `Terminal.Gui <ver>`). The TG version line is the smoke gate's authoritative source for which TG build the binary was linked against.
2. `clet list --json` validates against the schema.
3. For each input clet: TUIcast spawns with `--initial <stub> --json --timeout 1s` and a per-clet keystroke script that drives it to accept; verify exit 0 and JSON envelope.
4. For `md`: TUIcast spawns against a fixture markdown file with `"wait:500,q"`; verify exit 0 and `{"schemaVersion":1,"status":"ok"}`.
5. Cancellation: spawn with `--timeout 100ms`, no keystrokes; verify exit 130 and `{"schemaVersion":1,"status":"cancelled"}`.

**Asciinema artifact.** TUIcast captures every smoke run as `.cast`; on failure, the cast is uploaded as a workflow artifact. Release-failure forensics get a replay, not just a stack trace.

Any failure halts the publish workflow. The maintainer is paged via the release issue's auto-comment.

### 5.4 Publish steps

After all matrix jobs and smoke tests pass. **Channel determines which publish steps run:**

| Channel | Trigger | NuGet | Homebrew | WinGet | NuGet listing |
|---------|---------|:-----:|:--------:|:------:|---------------|
| Develop | `tg-develop-published` | ✅ | — | — | Prerelease (off `latest`) |
| Release | `tg-released`          | ✅ | ✅ | ✅ | Stable (latest)            |

The channel test is `if: ${{ !contains(env.TG_VERSION, '-') }}` for stable-only steps. NuGet's prerelease semantics surface develop builds only to consumers who pass `--prerelease`; `dotnet tool install -g Terminal.Gui.clet` continues to resolve the latest stable.

**Homebrew tap** (`gui-cs/homebrew-tap`) — *release channel only*:
- Generate `clet.rb` from `clet.rb.template` with new version + SHA256s for each bottle.
- PR (or push-with-token) to the tap repo.
- Verify with `brew install --build-bottle gui-cs/tap/clet` on a fresh runner.

**WinGet** (PR to `microsoft/winget-pkgs`) — *release channel only*:
- Generate manifests from templates with new version + installer URLs + SHA256s.
- Use `wingetcreate update` with the GitHub token.
- Manifest PR is auto-merged by Microsoft's bot if validation passes (otherwise paged).

**.NET tool** (NuGet) — *both channels*; follows the [mdv](https://github.com/gui-cs/mdv) packaging pattern (single project, `PackAsTool`):
- The `src/Clet/Clet.csproj` carries the tool metadata directly — no separate `Clet.Tool` project. Required properties: `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>clet</ToolCommandName>`, `<PackageId>Terminal.Gui.clet</PackageId>`, plus standard NuGet metadata (`Description`, `PackageReadmeFile`, `PackageLicenseFile`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageTags`). README and LICENSE are packed via `<None Include="..." Pack="true" PackagePath="/" />`.
- `dotnet pack -c Release src/Clet/Clet.csproj` produces `Terminal.Gui.clet.<version>.nupkg`.
- `dotnet nuget push Terminal.Gui.clet.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <key>`.
- End-user install: `dotnet tool install -g Terminal.Gui.clet`. The tool command is `clet` (independent of the package id, matching the `mdv` / `Terminal.Gui.mdv` pattern). The packed tool is the framework-dependent IL build; the AOT single-file binary is a separate artifact shipped through Homebrew/WinGet (see D-019).

### 5.5 Failure handling

If any publish step fails:
- The workflow opens an issue titled `Release v<TG_VERSION> failed (<channel>)`. Channel is `release` or `develop`.
- Labels reflect channel: release failures get `incident:release`, develop failures get `incident:develop`.
- **Pager behavior differs by channel.** Release failures page; develop failures don't (next develop publish supersedes within hours, and per-merge develops would page-spam). If develop incidents accumulate (>5/week), file a follow-up to investigate root cause.
- Already-published channels are noted; rollback is manual (we don't auto-revert).
- The smoke-test step ensures broken binaries never hit a channel; failures here are most often manifest/signing problems, not runtime regressions.

### 5.6 Version 1:1 with TG

clet's published version is the dispatch payload's `tg_version`, **verbatim including any prerelease suffix** (e.g. `2.0.2-develop.36` ⇒ clet `2.0.2-develop.36`; `2.1.0` ⇒ clet `2.1.0`). The workflow passes `-p:Version=${{ env.TG_VERSION }}` to both `dotnet pack` and `dotnet publish`. There is no version negotiation, no compatibility matrix, no "clet 1.5 supports TG 2.3+." The pair is locked.

The csproj also declares `<TerminalGuiVersion>` (defaulted to a known-good develop build for local development) and references TG via `<PackageReference Include="Terminal.Gui" Version="$(TerminalGuiVersion)" />`. The release workflow passes `-p:TerminalGuiVersion=${{ env.TG_VERSION }}` so the build pulls the exact TG version named by the dispatch — no floating range, no version drift between `tg_version` in the package label and what's actually linked. See [D-020](decisions.md#d-020).

---

## 6. Testing Plan

The user asked for thorough; this section is detailed accordingly. Eight test layers, each with a clear "what does this catch" purpose. All tests live in `gui-cs/clet/tests/`. `Markdown` View rendering quality is tested in TG core, not here (#5156).

### 6.0 When each layer runs

Two harnesses, two questions, two cost profiles. Keep them separate; don't merge.

- **In-process (xUnit + TG `InputInjection`)** answers *"is the View logic right?"* — runs in milliseconds, asserts on internal `IValue<T>` state, no Node toolchain. Layers §6.1, §6.2, §6.4, §6.7.
- **Process-level (TUIcast over PTY)** answers *"does the deployed binary behave right?"* — covers argument parsing, stdout JSON, exit codes, signal handling, AOT trim divergence. Slower (~1–2s per case). Layers §5.3, §6.3, §6.6.

Tier matrix:

| Trigger                       | Layers run                                                |
|-------------------------------|-----------------------------------------------------------|
| Inner loop (laptop)           | §6.1, §6.2                                                |
| PR CI                         | §6.1, §6.2, §6.3 (subset: happy path per clet), §6.4, §6.7 |
| Nightly                       | All of PR CI, plus full §6.3, §6.6, §6.8 dry-run          |
| Pre-release / release gate    | Full §6.3, §6.6, §5.3 smoke gate, §6.5 manual matrix      |

The legitimate worry that `InputInjection`-driven tests can drift from AOT behavior is addressed by §6.6 (AOT publish tests run the full §6.3 smoke matrix against the AOT binary). Don't double-pay it by routing §6.2 through TUIcast — the wall-clock cost is real and the marginal coverage is near zero.

### 6.1 Unit tests (`tests/Clet.UnitTests`)

**What this catches:** Logic bugs in the registry, options, parsing, JSON serialization, exit code mapping.

**Coverage target:** 90%+ for `src/Clet/Abstractions`, `src/Clet/Registry`, `src/Clet/Json`.

**Cases:**
- `CletRegistry`:
  - Register/resolve by primary alias, by secondary alias.
  - Conflict on duplicate alias raises `InvalidOperationException`.
  - `All` is stable in iteration order.
- `CletRunOptions`:
  - Default values.
  - `Fullscreen` flag round-trips correctly.
- `IParsable<T>` integration:
  - String → int, decimal, DateTime, TimeSpan via reflection-free hooks.
  - Bad input → `CletRunResult { Status = Error, ErrorCode = "validation" }`.
- `CletJsonOutput`:
  - Round-trip every result variant.
  - Output matches `SchemaV1` byte-for-byte for canonical inputs (golden files).
  - No properties leak: cancelled envelopes contain only `schemaVersion` and `status`; viewer success envelopes contain only `schemaVersion` and `status`; no envelope ever emits a wire-format `type` field (the field was dropped at v0.5; result types are advertised once via `clet list --json`, not on every result).
- Exit code mapping:
  - Each `CletRunStatus` and error code maps to the documented exit.
- Cancellation:
  - `CletRunResult.Cancelled` propagates through every layer.

**Per-clet behavior tests** (one fixture per clet; 15 total):
- Register, resolve, advertise correct `Kind` and `ResultType`.
- Default options round-trip.
- Initial-value parsing with valid input.
- Initial-value rejection with invalid input.
- (Where applicable) options: `--root`, `--filter`, `--multi`, etc., each tested in isolation.

**Patterns:** xUnit v3, `[Fact]` and `[Theory]`. No `Application.Init`. Each test file leads with `// Claude - Opus 4.7` per `CLAUDE.md`.

### 6.2 Integration tests (`tests/Clet.IntegrationTests`)

**What this catches:** TG hosting bugs (init/teardown, cancellation, rendering) that unit tests can't see because they don't run a real run loop.

**Cases:**
- Run each clet end-to-end against a scripted input/output stream.
- Cancellation token cancels mid-run; verify final result and clean shutdown.
- Timeout fires; verify `Status = Cancelled` and exit 130.
- Theme override per invocation; verify View's effective scheme.
- Inline vs alt-screen mode; verify driver state transitions.

**Test harness:** `IApplication` instance per test, keystrokes synthesized via Terminal.Gui's `InputInjection` mechanism (the canonical TG-side hook for posting key/mouse events into a running loop), captured render output (snapshot to string). In-process injection is the right tool here — there is no subprocess to drive — and complements the process-level TUIcast harness used by §5.3 and §6.3.

**Snapshot infrastructure: reuse TG's, don't roll our own.** TG already maintains snapshot-rendering helpers, and #5156 adds golden-file rendering for the `Markdown` View. Clet integration tests consume that same harness. If the harness is missing a hook clet needs, file against TG core; do not fork. Two parallel snapshot stacks would drift within a release.

### 6.3 Process/smoke tests (`tests/Clet.SmokeTests`)

**What this catches:** Bugs that only appear when `clet` runs as a real process (argument parsing, stdout/stderr wiring, exit codes, signal handling).

**Cases:** Identical to §5.3 release-pipeline smoke tests (every clet boots, returns valid JSON, exits with correct code). Run on every PR to `gui-cs/clet`, every TG-triggered release build, and nightly against the latest TG develop branch.

**Tooling:** TUIcast in deterministic-script mode (same driver as §5.3). The xUnit fixture shells out to TUIcast with a per-clet keystroke script, captures the resulting JSON from the spawned `clet` process's stdout, and asserts on exit code + envelope shape. Using the same driver as the release gate means a green CI run is byte-equivalent evidence that the release gate will be green; we do not maintain two parallel smoke harnesses.

**Scope guardrail.** §6.3 covers exactly one happy path per clet plus the cancellation case. Bug repros, option-matrix coverage, and behavior variants go in §6.2 (in-process, fast). Without this rule, every regression PR adds a TUIcast case, the release gate hits 30 minutes by v0.7, and the smoke layer becomes the integration layer at process-level cost.

**Scripts as data, not code.** Per-clet keystroke scripts live as text files under `tests/Clet.SmokeTests/scripts/<alias>.txt` (one line per script, TUIcast comma-separated keystroke syntax). The xUnit fixture loads the file by alias; it does not embed scripts in C# string literals. A contributor can add a smoke case by editing data, and any script can be re-run locally with `npx tuicast --binary ./clet --script-file scripts/<alias>.txt` without rebuilding the test project.

**Asciinema artifacts.** TUIcast captures every smoke run as a `.cast`. Successful runs discard the cast (artifact-store noise); failed runs upload it as a workflow artifact for forensic replay. Retention follows GitHub Actions' default (90 days). If we ever need longer for a specific incident, copy the artifact to the issue manually.

### 6.4 JSON contract tests (`tests/Clet.ContractTests`)

**What this catches:** Schema drift; promises to AI agent consumers being broken silently.

**Cases:**
- Every line emitted by every clet across the full input matrix validates against `SchemaV1`.
- Schema additions in v1.x are confirmed additive only (a v1.0 consumer can still parse v1.x output).
- `clet list --json` validates against its own list schema.

**Tooling:** `JsonSchema.Net` for validation. The schema file is the source of truth; tests read it, not a copy.

### 6.5 Cross-terminal manual matrix

**What this catches:** Driver-specific rendering bugs that automated tests can't reproduce reliably (cursor save/restore, alt-screen toggles, mouse).

**Matrix:**

|                  | macOS Terminal | iTerm2 | Windows Terminal | GNOME Terminal |
|------------------|:--------------:|:------:|:----------------:|:--------------:|
| `clet text`      |       ☐        |   ☐    |        ☐         |       ☐        |
| `clet pick-file` |       ☐        |   ☐    |        ☐         |       ☐        |
| `clet md`        |       ☐        |   ☐    |        ☐         |       ☐        |
| Theme switch     |       ☐        |   ☐    |        ☐         |       ☐        |
| Mouse click      |       ☐        |   ☐    |        ☐         |       ☐        |
| Inline restore   |       ☐        |   ☐    |        ☐         |       ☐        |

Run before every minor release (v1.0, v1.1, ...). Captured in a release checklist issue ([#23](https://github.com/gui-cs/clet/issues/23) tracks the first pass for v0.5). This is the v0.5 milestone gate.

### 6.6 AOT publish tests

**What this catches:** Trim warnings, runtime AOT failures, regressions in AOT-compatibility of TG core. With no separate AOT audit (the original §3 entry was dropped because TG core already tracks AOT work), these tests are the primary discovery mechanism for AOT issues; failures here are filed as issues against `gui-cs/Terminal.Gui` with a minimal repro.

**Cases:**
- CI publishes the AOT binary on every PR to `gui-cs/clet` and on the nightly TG-develop run.
- Zero trim warnings tolerated; warnings fail the build.
- Smoke tests (§6.3) run against the AOT binary, not just the JIT'd debug build.
- AOT failures discovered during `gui-cs/clet` builds are filed against `gui-cs/Terminal.Gui` with a minimal repro.

### 6.7 Performance tests (`tests/Clet.PerfTests`)

**What this catches:** Cold-start regressions that erode the "feels instant" property AI agents need.

**Cases:**
- `clet --version` cold start: <100ms macOS arm64, <150ms Windows x64.
- `clet list --json` cold start: same budgets.
- Tracked over time; regression alerts at +25% on a 7-day rolling baseline.

### 6.8 Release pipeline dry-run tests

**What this catches:** Workflow regressions that would otherwise only surface during a real TG release (when the cost is high).

**Cases:**
- Weekly cron: simulate a `repository_dispatch` with a fake version. Build, smoke-test, generate manifests, but stop short of publish.
- Verify all template files render correctly, all artifact uploads succeed, all checksums match.

---

## 7. Milestones

Schedule follows TG releases, not a calendar; no dates here.

| Milestone | Tracking | Exit criteria |
|-----------|----------|---------------|
| **v0.1 alpha** | [#2](https://github.com/gui-cs/clet/issues/2) | `gui-cs/clet` repo bootstrapped; abstractions, registry, JSON, source generator in place; `select` clet (replicating `Examples/InlineSelect`) working in unit + integration tests. **No runnable binary yet** — see v0.11. |
| **v0.11** | [#9](https://github.com/gui-cs/clet/issues/9) | Runnable `clet` binary. CLI host (`Program.Main`, `CommandLineRoot`, `AliasDispatcher`, `OutputFormatter`, `ExitCodes`) per §4.6/§4.7. `clet --help` / `--version` / `help <alias>` / `list --json` / `<alias> --json` work end-to-end. Plain-text help; Markdown-rendered help defers to v0.5. Process-level smoke harness on Linux x64 (Process.Start-based; TUIcast keystroke harness deferred to v0.3 — see [decisions log D-007](decisions.md)). |
| **v0.3 alpha** | [#3](https://github.com/gui-cs/clet/issues/3) | All 14 input clets functional. JSON schema drafted. AOT publish (§6.6) green on `gui-cs/clet` CI. TUIcast keystroke harness wired up. |
| **v0.5 beta** | [#4](https://github.com/gui-cs/clet/issues/4) | Naming locked; JSON schema locked; exit-code table locked; inline rendering verified on the four-terminal matrix; v1.0 input and viewer lists locked; `Markdown` View integration verified end-to-end including link safety; threat model published; `dotnet tool install -g Terminal.Gui.clet` packs and installs locally (mdv pattern, see D-019); Homebrew tap and WinGet manifest in working draft form; **continuous-release loop wired up — every TG develop NuGet drives a clet prerelease push, every TG release tag drives a stable push** (D-020, supersedes the earlier "TG dep on a release tag" criterion); release workflow proven end-to-end against a real TG develop publish (`Terminal.Gui.clet 2.0.2-develop.35` shipped autonomously) — the release-tag half stays open until TG cuts an actual tag. |
| **v0.75 alpha** | [#33](https://github.com/gui-cs/clet/issues/33) | Friends-and-family alpha. ≥5 external testers have installed via a published channel and run a non-trivial flow; ≥3 alpha-feedback Issues filed by non-maintainers (alpha feedback = GitHub Issues, no Discussions); README points testers at the Issues tracker; ≥2 of the 4 target terminals driven by someone other than the maintainer; both stable and `--prerelease` install paths exercised end-to-end; maintainer dogfooding `clet` (not `dotnet run`) in real workflows for ≥2 weeks; ≥1 AI agent harness consuming `clet --json` for a non-toy task; all P0 alpha bugs resolved or explicitly deferred to v0.9. **Not v0.9 RC** — the four-terminal matrix run, full smoke-gate coverage, and the rollback runbook exercise are still v0.9 gates. |
| **v0.9 RC** | [#5](https://github.com/gui-cs/clet/issues/5) | All §6 test layers passing in CI. One real release cycle exercised end-to-end. Rollback runbook (`docs/runbooks/release-rollback.md`) exercised once. Alpha-feedback (v0.75) P1/P2 bugs triaged; ones marked for v0.9 are resolved here. |
| **v1.0 GA** | [#6](https://github.com/gui-cs/clet/issues/6) | Tied to TG v2 GA. Brew, WinGet, NuGet channels live. Documentation published. Issue templates for clet bugs in place. |

---

## 8. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------:|-------:|------------|
| AOT issue surfaces during `gui-cs/clet` build or smoke test | Medium | Medium | §6.6 catches before publish; file against TG core; if blocking on a release, fall back to self-contained single-file (~30MB) and document. |
| `FileDialog` typed-result refactor (§3.2) breaks downstream callers | Low | Medium | Coordinate with TG core team; flag as breaking in release notes; fix in-tree callers as part of the PR. |
| Native installer pipeline (Homebrew/WinGet) ops cost | Medium | Medium | §5.3 smoke gate + §6.8 dry-runs catch most issues pre-publish; `docs/runbooks/release-rollback.md` documents the response when a regression slips the gate and a published artifact must be withdrawn. |
| Markdown View quality regression vs `glow` | Low | Medium | TG-side golden-file corpus (#5156); quarterly comparison run. |
| ~~TG `develop` carries #5157/#5158 but no release tag yet; clet pins to a `Terminal.Gui` `*-develop.*` preview NuGet~~ | ~~High~~ | ~~Low~~ | **Resolved by D-020.** csproj uses `<PackageReference Version="$(TerminalGuiVersion)" />` defaulting to a known-good develop build; the release workflow overrides this from the dispatch payload. There is no longer a "pinned develop" — the pair is whatever was dispatched. |
| Develop publishes per TG merge create NuGet version sprawl | Medium | Low | NuGet handles the volume (TG itself does this); prerelease semantics keep develop builds off `latest`. NuGet versions are immutable so cleanup isn't possible — but isn't needed either. |
| First real `repository_dispatch` release fails mid-publish (one or more channels published before the failure) | Medium | High | §6.8 weekly dry-runs catch workflow regressions; `docs/runbooks/release-rollback.md` walks through the per-channel withdrawal procedure (Homebrew tap revert, WinGet manifest removal PR, NuGet unlist). Runbook exercised once before v0.9 RC. |
| Naming concerns about "clet" surfacing in support channels | Low | Low | Acknowledge in docs; outlast. |

---

## 9. Open Questions

1. **Telemetry.** The PR/FAQ mentions an opt-in usage ping. Spec deliberately does not include this in v1.0 scope; revisit at v1.1 with a privacy review.
2. **Homebrew tap repo name.** `gui-cs/homebrew-tap` is assumed; confirm it exists or create.
3. **Code signing certs.** Apple Developer ID and Authenticode certs are operational dependencies; confirm ownership/renewal process before v0.9.
4. **`md` content source.** ~~File argument (`clet md README.md`), stdin (`cat README.md | clet md -`), or both?~~ **Resolved (D-015).** Both file arguments and stdin, with precedence: file args → `--initial` inline content → stdin → error.
5. **PR/FAQ update upstream.** Issue #5155's PR/FAQ still references `Terminal.Gui.Clets` as a separate assembly (Tig's quote, the strategic FAQ). Update the issue body to match this spec before v0.5. (This repo's own README has been corrected to match.)

---

## 10. Implementation Order

A suggested sequence (linear, not parallelizable until v0.3 except where noted):

1. **TG-side prerequisites — status:**
   - **#5157 `Application.RunAsync(Toplevel, CancellationToken)`** — **landed on `develop`**. Clet binds directly via the pinned `Terminal.Gui` `*-develop.*` preview NuGet (see §3 preamble, §8 risks).
   - **#5158 `FileDialog` typed-result refactor** — **landed on `develop`** as `Dialog<IReadOnlyList<string>?>`. Wave 4 (`pick-file`/`pick-directory`) is unblocked.
   - **#5156 `Markdown` View golden-file rendering tests** — outstanding; independent quality work, prerequisite for the v0.5 `clet md` link-safety verification but not for any earlier milestone.

   The `*-develop.*` pin issue is resolved by D-020 — the csproj uses a `$(TerminalGuiVersion)` MSBuild variable, set at build time from the dispatch payload.

2. **`gui-cs/clet` repo bootstrapped:** solution layout, abstractions, registry, JSON, source generator. CI on push (build, unit tests).
3. **First clet: `select`.** A direct port of `Examples/InlineSelect/Program.cs` to a `SelectClet : IClet<int?>` using `RunnableWrapper<OptionSelector, int?>`. End-to-end through unit + integration tests + a manual run from a real shell. This proves the entire pipeline (registry, alias dispatch, output formatter, exit codes, JSON schema) on the simplest non-trivial clet shape.
4. **CLI host.** Program.Main, System.CommandLine, alias dispatch, output formatter. Smoke test harness (§6.3) running on a single RID.
5. **Second wave of clets:** `text`, `confirm`, `int`, `decimal`. These exercise `IParsable<T>`-based initial-value parsing across the most common scalar types.
6. **Third wave:** `multi-select`, `range`, `date`, `time`, `duration`, `color`, `attribute-picker`. Covers more `IValue<T>` shapes and the more complex Views.
7. **Fourth wave:** `pick-directory`, `pick-file`. #5158 has landed on TG `develop` (§3.2); `FileDialog` typed result is `IReadOnlyList<string>?` (string for single-select wire format, array of strings for `--multi`, per §4.3.2).
8. **`md` viewer clet.** First viewer clet; exercises the `IViewerClet` contract and the help-rendering pipeline (§4.7).
9. **Release pipeline:** build matrix, signing, smoke gate.
10. **`dotnet tool` packaging:** add `PackAsTool`/`ToolCommandName`/`PackageId=Terminal.Gui.clet` to `src/Clet/Clet.csproj` (mdv pattern, D-019). `dotnet pack -c Release` produces a global-tool `.nupkg`; `dotnet tool install -g --add-source ./bin/Release Terminal.Gui.clet` works locally end-to-end. This is the lowest-friction install path and the first publish channel exercised before Homebrew/WinGet.
11. **Publish channels:** Homebrew (lowest ops friction for native AOT bottle), then WinGet, then NuGet tool push.
12. **v0.5 gate:** four-terminal matrix run + threat model + locked schema + #5156 Markdown rendering tests landed in TG.
13. **v0.75 alpha — friends-and-family testing.** Point the README at the Issues tracker for alpha feedback (no Discussions), recruit ≥5 external testers, dogfood for ≥2 weeks, run an AI agent harness against `--json`. P0 bugs resolved or explicitly deferred. Surface area is what was locked in v0.5 — don't add features here. ([#33](https://github.com/gui-cs/clet/issues/33))
14. RC and GA.

---

## Appendix A: Threat Model Summary

(Full document published with v0.5; sketch only here.)

- **Untrusted inputs:** `--initial`, env vars, stdin content, fixture file paths, `--title`, clet-specific options.
- **Sanitization:** All output to stdout/stderr passes through a terminal-escape filter (strip C0/C1 control sequences except those we generate). User-controlled display strings (`--title`, prompt labels) sanitized at the View boundary.
- **Markdown link policy:** Default `SurfaceOnly` (links shown, never auto-opened). `--allow-link-open` flag for the user to opt in; off by default for AI agent use.
- **File access:** `pick-file` and `pick-directory` honor the OS sandbox/permission model; no privilege escalation.
- **Plugin loading:** None in v1.0. (Closes the entire LoadFrom-based attack surface.)

## Appendix B: Cross-References

- PR/FAQ: [issue #5155](https://github.com/gui-cs/Terminal.Gui/issues/5155)
- TG core docs: `docfx/docs/application.md`, `docfx/docs/View.md`, `docfx/docs/cancellable-work-pattern.md`
- Contributor rules: `.claude/rules/`, `CLAUDE.md`
