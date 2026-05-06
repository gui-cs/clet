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

- **`src/Clet/`** — The CLI executable (net10.0). Depends on `Terminal.Gui` v2 (preview NuGet, currently `2.0.2-develop.24` — pin tracked in `src/Clet/Clet.csproj`, must be replaced with a release tag before v0.5 schema-lock per spec §8 risks). All abstractions are `internal` (not published until v2 plugin system).
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

- **`specs/clet-spec.md`** — authoritative design document. Planned clets, exit code semantics, JSON schema (§4.3 + §4.3.1 versioning + §4.3.2 per-clet shapes), milestone roadmap (§7), risks (§8), open questions (§9), auto-discovery exploration (§11). Where the spec and current code diverge, the decisions log says why. **§6 (Testing) is a thin pointer** to `tests/SPEC.md`.
- **`tests/SPEC.md`** — authoritative testing strategy. Nine test layers, tier matrix (which layers run when), per-layer cases, harness shapes, golden-file conventions. Lives next to the test projects so it stays in sync. The main spec defers to it for everything in `tests/`.
- **`specs/decisions.md`** — append-only log of cross-cutting decisions that don't fit cleanly into one spec section. *Why* the parser is hand-rolled (D-006), *why* TUIcast is deferred to v0.3 (D-007), *why* the JSON envelope dropped its `type` field (D-001), etc. **Read this before "fixing" something to match the spec** — the deviation may be deliberate. New decisions append; supersede, don't edit.
- **[Bar-raise backlog issue #11](https://github.com/gui-cs/clet/issues/11)** (label `bar-raise`) — critique that's been raised, considered, and *not yet* acted on. Before claiming a section is "done," check the backlog for queued items in that area. New design pushback goes there, not into a side conversation.
- **`docs/runbooks/release-rollback.md`** — operational runbook for withdrawing a bad release across Homebrew/WinGet/NuGet. Draft until exercised at v0.9.

### Doc-update gate for every PR

Before a PR can be merged, the docs above must reflect what the PR changes. This isn't a stylistic preference — it's how the three-document split (intent / decisions / backlog) stays trustworthy. **A PR that ships behavior without updating the right doc is incomplete, even if all tests pass.**

Use this checklist on every PR. **The spec and the decisions log must agree on every claim** — if you touch one, audit the other in the same PR.

- **Did the PR change CLI surface, exit codes, JSON envelope, or any user-visible behavior the spec describes?** → Update `specs/clet-spec.md` in the same PR. If the change *contradicts* something in the spec (rather than refining it), also add a decisions-log entry explaining why.
- **Did the PR change a test project's layout, harness shape, or layer scope?** → Update `tests/SPEC.md` in the same PR. The main spec's §6 is a pointer; the substance lives in `tests/SPEC.md`.
- **Did the PR add or update a `D-NNN` entry in `specs/decisions.md`?** → **Audit the spec and the README for anything that now disagrees with the new entry, and update them in the same PR**, pointing the affected section at the `D-NNN` entry. The decisions log captures *why* a deviation exists; the spec/README must still describe *what currently ships*. A `D-NNN` that says "we return text not indices" while §4.3.2 still says "integer (zero-based index)" is a worse failure than no decision entry at all — it makes the spec actively misleading. Concrete trigger list: any new/updated entry whose Context or Decision section quotes the spec, references a press-release claim, or names a section like "§4.3", "§4.7", "§5.4", "Appendix A".
- **Did the PR make a non-obvious choice a future reader might want to "fix"?** (workarounds against upstream bugs, deliberate divergence from the spec, hand-rolled where a library exists, deferred mechanism, load-bearing handler that looks redundant) → Append a new entry to `specs/decisions.md` with `## D-NNN: <title> (Active)`. Append, don't edit; supersede with a new entry if a prior decision is reversed. Then re-run the bullet above — adding a `D-NNN` is itself a trigger for the spec/README audit.
- **Did the PR resolve or invalidate an item on bar-raise issue #11?** → Tick the checkbox or add a follow-up note on the issue.
- **Did the PR complete a checkbox on a milestone tracking issue (#2/#9/#3/#4/#5/#6)?** → Tick the box on the issue itself, not just locally.
- **Did the PR change release/operational steps?** → Update `docs/runbooks/release-rollback.md` (or add a new runbook under `docs/runbooks/`).

If none of the above apply, say so explicitly in the PR description ("no spec/decisions/runbook impact") rather than leaving it implicit. Reviewers should reject PRs that ship surface-level changes without the corresponding doc update — and reject PRs that add a `D-NNN` entry without the matching spec/README sync, since that strands the spec in a misleading state.

### Milestones

Tracked as GitHub issues with `milestone` + `tracking` labels. Spec §7 cross-references each:

- **#2 v0.1 alpha** — library + `select`, in-process integration test (no runnable binary).
- **#9 v0.11** — runnable binary, CLI host, plain-text help, process-level smoke (5/6 cases).
- **#3 v0.3 alpha** — all 14 input clets + AOT publish + TUIcast keystroke harness.
- **#4 v0.5 beta** — schema/exit-code/naming locks; threat model; release workflow proven; **TG dep on a release tag, not develop**.
- **#5 v0.9 RC** — full test matrix green; one real release cycle exercised; rollback runbook exercised.
- **#6 v1.0 GA** — tied to TG v2 GA; brew/winget/nuget channels live.

Each issue's checkboxes are the source of truth for "is this milestone done." When you complete an item, tick the box in the issue, not just in your head.
