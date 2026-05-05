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

- **`src/Clet/`** — The CLI executable (net10.0). Depends on `Terminal.Gui` v2 (preview NuGet). All abstractions are `internal` (not published until v2 plugin system).
- **`src/Clet.SourceGen/`** — Roslyn source generator for static clet registration (planned `[Clet]` attribute).
- **`tests/Clet.UnitTests/`** — Registry, JSON schema, clet metadata tests.
- **`tests/Clet.IntegrationTests/`** — Tests that init Terminal.Gui (`Application.Create()`, `app.Init("ansi")`).

### Key directory layout inside `src/Clet/`

- `Abstractions/` — `IClet`, `IClet<TValue>`, `IViewerClet`, `ICletRegistry`, `CletKind`, `CletRunResult<T>`, `CletRunOptions`, `CletOptionDescriptor`
- `Registry/` — `CletRegistry` (instance-based, case-insensitive alias lookup, duplicate protection)
- `Json/` — `SchemaV1` (the JSON envelope) and `CletJsonContext` (source-generated System.Text.Json)
- `Clets/Input/` — Input clet implementations (currently `SelectClet`)
- `Clets/Viewer/` — Viewer clet implementations (planned: `md`)
- `Hosting/` — `Program.cs` entry point

### Core patterns

**Two clet kinds:** Input clets wrap a View with `IValue<T>` and return a typed result via `Task<CletRunResult<TValue>> RunAsync(...)`. Viewer clets are read-only (dismissable with Esc/q/Ctrl-C) and return status-only envelopes.

**JSON result envelope (SchemaV1):** `{ schemaVersion: 1, status, value?, code?, message? }`. Status is one of: `ok`, `cancelled`, `error`, `no-result`. Value is omitted (not null) when absent.

**Exit codes:** 0 = success, 2 = usage error, 130 = cancelled (SIGINT convention).

**Registry:** Instance-based (not static singletons). Tests can create isolated registries.

**InternalsVisibleTo:** Both test projects have access to internals via `src/Clet/Properties/AssemblyInfo.cs`.

### Integration test conventions

Tests use `Application.Create()` for isolated contexts, `app.Init("ansi")` for terminal init, and `app.StopAfterFirstIteration = true` for non-blocking execution. Pre-cancelled `CancellationToken` tests verify cancellation semantics without blocking.

## Spec

The implementation spec at `specs/clet-spec.md` is the authoritative design document. Consult it for planned clets, exit code semantics, JSON schema details, and milestone roadmap.
