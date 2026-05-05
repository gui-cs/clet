# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is clet

`clet` is a CLI tool that exposes Terminal.Gui Views as shell commands with typed, JSON-serializable results. It targets shells, scripts, and AI agents. The binary is a thin host: parse args, look up a clet alias in the registry, init Terminal.Gui, call `RunAsync`, serialize the result, exit. Currently at v0.1-alpha with one clet (`select`); v1.0 targets 14 input clets and 1 viewer clet (`md`).

## Build and Test

.NET 10.0 preview. Solution file is `Clet.slnx` (modern format).

```bash
dotnet restore
dotnet build --no-restore

# Tests use xunit.v3 via dotnet run (not dotnet test)
dotnet run --project tests/Clet.UnitTests --no-build
dotnet run --project tests/Clet.IntegrationTests --no-build
```

There is no separate lint step. CI runs on ubuntu-latest with `dotnet-quality: preview`.

## Architecture

Four projects in two repos (this repo only contains the `clet` side):

- **`src/Clet/`** — The CLI executable (net10.0). Depends on `Terminal.Gui` v2 (preview NuGet, currently `2.0.2-develop.21` — pin tracked in `src/Clet/Clet.csproj`, must be replaced with a release tag before v0.5 schema-lock per spec §8 risks). All abstractions are `internal` (not published until v2 plugin system).
- **`src/Clet.SourceGen/`** — Roslyn source generator for static clet registration (planned `[Clet]` attribute). Currently a placeholder; `BuiltInClets.RegisterAll` is hand-written until the generator earns its keep — see `specs/decisions.md` D-004.
- **`tests/Clet.UnitTests/`** — Registry, JSON schema, host pipeline (CommandLineRoot, OutputFormatter, ExitCodes, BuiltInClets) tests.
- **`tests/Clet.IntegrationTests/`** — In-process tests that init Terminal.Gui (`Application.Create()`, `app.Init("ansi")`).
- **`tests/Clet.SmokeTests/`** — Process-level smoke tests (`Process.Start` against the built `Clet.dll`). The keystroke-driven cases land at v0.3 with TUIcast — see `specs/decisions.md` D-007.

### Key directory layout inside `src/Clet/`

- `Abstractions/` — `IClet`, `IClet<TValue>`, `IViewerClet`, `ICletRegistry`, `CletKind`, `CletRunResult<T>`, `CletRunOptions`, `CletOptionDescriptor`, `BoxedCletResult` (non-generic dispatch type — see decisions D-005)
- `Registry/` — `CletRegistry` (instance-based, case-insensitive alias lookup, duplicate protection); `BuiltInClets.RegisterAll` (manual registration; D-004)
- `Json/` — `SchemaV1` (the JSON envelope) and `CletJsonContext` (source-generated System.Text.Json)
- `Clets/Input/` — Input clet implementations (currently `SelectClet`)
- `Clets/Viewer/` — Viewer clet implementations (planned: `md`)
- `Hosting/` — `Program.cs` entry point, `CommandLineRoot` (hand-rolled CLI parser; D-006), `AliasDispatcher`, `OutputFormatter`, `ExitCodes`

### Core patterns

**Two clet kinds:** Input clets wrap a View with `IValue<T>` and return a typed result via `Task<CletRunResult<TValue>> RunAsync(...)`. Viewer clets are read-only (dismissable with Esc/q/Ctrl-C) and return status-only envelopes.

**JSON result envelope (SchemaV1):** `{ schemaVersion: 1, status, value?, code?, message? }`. Status is one of: `ok`, `cancelled`, `error`, `no-result`. Value is omitted (not null) when absent.

**Exit codes:** 0 = success, 2 = usage error, 130 = cancelled (SIGINT convention).

**Registry:** Instance-based (not static singletons). Tests can create isolated registries.

**InternalsVisibleTo:** Both test projects have access to internals via `src/Clet/Properties/AssemblyInfo.cs`.

### Integration test conventions

Tests use `Application.Create()` for isolated contexts, `app.Init("ansi")` for terminal init, and `app.StopAfterFirstIteration = true` for non-blocking execution. Pre-cancelled `CancellationToken` tests verify cancellation semantics without blocking.

## Spec, decisions, and backlog

The repo intentionally splits "design intent" from "current code" from "queued critique" across three places. Read all three before assuming you know how a piece fits.

- **`specs/clet-spec.md`** — authoritative design document. Planned clets, exit code semantics, JSON schema (§4.3 + §4.3.1 versioning + §4.3.2 per-clet shapes), test plan (§6 with the §6.0 tier matrix), milestone roadmap (§7), risks (§8), open questions (§9). Where the spec and current code diverge, the decisions log says why.
- **`specs/decisions.md`** — append-only log of cross-cutting decisions that don't fit cleanly into one spec section. *Why* the parser is hand-rolled (D-006), *why* TUIcast is deferred to v0.3 (D-007), *why* the JSON envelope dropped its `type` field (D-001), etc. **Read this before "fixing" something to match the spec** — the deviation may be deliberate. New decisions append; supersede, don't edit.
- **[Bar-raise backlog issue #11](https://github.com/gui-cs/clet/issues/11)** (label `bar-raise`) — critique that's been raised, considered, and *not yet* acted on. Before claiming a section is "done," check the backlog for queued items in that area. New design pushback goes there, not into a side conversation.
- **`docs/runbooks/release-rollback.md`** — operational runbook for withdrawing a bad release across Homebrew/WinGet/NuGet. Draft until exercised at v0.9.

### Milestones

Tracked as GitHub issues with `milestone` + `tracking` labels. Spec §7 cross-references each:

- **#2 v0.1 alpha** — library + `select`, in-process integration test (no runnable binary).
- **#9 v0.11** — runnable binary, CLI host, plain-text help, process-level smoke (5/6 cases).
- **#3 v0.3 alpha** — all 14 input clets + AOT publish + TUIcast keystroke harness.
- **#4 v0.5 beta** — schema/exit-code/naming locks; threat model; release workflow proven; **TG dep on a release tag, not develop**.
- **#5 v0.9 RC** — full test matrix green; one real release cycle exercised; rollback runbook exercised.
- **#6 v1.0 GA** — tied to TG v2 GA; brew/winget/nuget channels live.

Each issue's checkboxes are the source of truth for "is this milestone done." When you complete an item, tick the box in the issue, not just in your head.
