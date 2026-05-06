# clet design decisions log

Cross-cutting decisions that don't fit cleanly into a single milestone issue or a single spec section. New entries go on top. Each entry is short by design — context, decision, status, and a pointer to where it took effect.

When a decision changes, **don't edit the entry** — add a new one above it that supersedes the old, and mark the old one `Superseded by #N`. The log is append-only so future agents can see what was tried.

Format: `## D-NNN: <short title> (status)`. Status is one of `Active`, `Superseded by D-NNN`, `Reversed`, or `Pending`.

## D-022: Input-size caps to prevent OOM from untrusted input (Active)

**Context.** Appendix A of the spec names `--initial`, env vars, and stdin as untrusted inputs but specified no length caps. An agent piping a 4 GB log into `clet md -` would OOM the binary with no error message or exit code — just a dead process. Issue #38.

**Decision.** Cap `--initial` at 64 K characters (enforced in `CommandLineRoot` argument parsing). Cap `clet md` stdin at 8 M characters (enforced in `MarkdownClet`'s content resolver). On exceed: exit 65 (validation), error code `input-too-large`, JSON envelope `{"schemaVersion":1,"status":"error","code":"input-too-large","message":"..."}`. Both caps are documented in spec §4.7 and Appendix A. Caps are measured in .NET `char` count (UTF-16 code units), not bytes — this is faster and provides a solid OOM-protection bound even though the actual byte footprint varies with encoding. Per-clet options (`cletOptions` values) are not yet capped; tracked as a follow-up.

**Status.** Active.

**Pointers.** `src/Clet/Hosting/CommandLineRoot.cs` (MaxInitialChars constant + guard), `src/Clet/Clets/Viewer/MarkdownClet.cs` (MaxStdinChars + length-limited read), `specs/clet-spec.md` §4.7 + Appendix A.

## D-021: Auto-discovered clets ("any IValue<T> View just works") deferred to v2 (Active)

**Context.** The original PR/FAQ pitched clet as a way to expose any Terminal.Gui View with `IValue<T>` to the shell automatically. v1.0 ships 15 hand-written clets instead. Spec §11 lays out what we learned, what full auto-discovery would require on both the TG side (a `[Shellable]` attribute or marker, wire-format declaration, initial-value parser, per-View option surface) and the clet side (a real source generator), and the cross-cutting cost (TG core would need to host clet-shaped opinions, schema-lock would couple to TG's `[Shellable]` surface).

**Decision.** Don't pursue full auto-discovery in v1.x. Hand-written `BuiltInClets.RegisterAll` continues through v1.0. `Clet.SourceGen` stays as a placeholder (don't delete — it's a parking spot for the v2 reopen). The `[Clet("alias", typeof(TResult))]` attribute sketched in spec §4.5 is illustrative only; shipped code uses plain `IClet<TValue>` interface implementation. Revisit at v2 if and when third-party clets become a goal — at which point the TG-side asks (§11.3 A–E) become a co-design topic with TG core, with §11 as the starting checklist.

**Why:** 15 clets at ~50–150 lines each ≈ 1500 LOC of mostly-metadata is not the bottleneck. Cross-cutting concerns (`--title`, scheme, link safety, exit codes) already live above the per-clet layer; auto-discovery wouldn't change that. The leverage of full auto-discovery only kicks in if a long tail of new TG Views or third-party Views want shell exposure post-v1.0 — both are explicitly out of scope today (§1, plugin loading exclusion in Appendix A). And introducing a `[Shellable]` attribute on TG core softens the §2 "nothing in TG core knows about clets" decision; that's a TG-side opinion-shift we shouldn't ask for without the v2 third-party-clets driver behind it.

**How to apply:** Spec §11 is the canonical exploration. D-004 (source generator deferred) is **superseded by this entry** — D-004's "Pending — revisit before v0.3 GA" is now closed: the answer is "don't bother in v1.x." Bar-raise [#BR-11](https://github.com/gui-cs/clet/issues/11) ticked. v1.x refinements that pay down clet boilerplate without locking in a TG-side commitment (Options-declaration builder helper, generated §4.3.2 wire-format table, contract test for wire-format conformance) are listed in §11.5 and remain candidates for separate PRs.

**Status.** Active. Supersedes D-004.

**Pointers.** Spec §11 (full exploration), §11.5 (recommendation + v1.x refinements), §11.6 (open questions for v2). `src/Clet.SourceGen/` retained as placeholder. `src/Clet/Registry/BuiltInClets.cs` continues as hand-written. Bar-raise issue [#11](https://github.com/gui-cs/clet/issues/11) #BR-11.

## D-020: Continuous-release loop on TG develop + release; channel from version suffix (Active)

**Context.** Spec §5.1 originally fired clet's release workflow on a single trigger: `repository_dispatch type=tg-released` from a TG release tag. That left the §8 develop-pin risk wide open — clet had to hand-pin `Terminal.Gui Version="2.0.2-develop.NN"` and bump manually whenever TG develop changed. It also left clet silent during the long stretches between TG releases, even when develop carries shippable improvements. We want clet to track TG continuously (every develop NuGet publish drives a clet prerelease) **and** still produce stable artifacts on TG release tags (Homebrew, WinGet, NuGet "latest"). See [issue #30](https://github.com/gui-cs/clet/issues/30) for the kicked-off plan.

**Decision.**

1. **Two dispatch types.** TG fires `tg-released` on release tags and `tg-develop-published` on every develop NuGet publish. Both carry `client_payload.tg_version`. clet's workflow accepts both.
2. **Channel from version suffix.** `tg_version` containing `-` ⇒ develop channel; otherwise ⇒ release channel. No separate `channel` field needed — the version string already carries the signal, and SemVer prerelease semantics line up with NuGet's listing rules.
3. **Per-channel publishing.** NuGet runs on both channels; Homebrew and WinGet run on release only. NuGet's prerelease semantics ensure `dotnet tool install -g Terminal.Gui.clet` resolves to stable; `--prerelease` opts into develop.
4. **clet version = TG version verbatim.** No version negotiation, no compatibility matrix; the workflow passes `-p:Version=${{ env.TG_VERSION }}` to pack/publish.
5. **Replace the hard-pinned `<PackageReference>` with an MSBuild variable.** `<PackageReference Include="Terminal.Gui" Version="$(TerminalGuiVersion)" />`, with `<TerminalGuiVersion>` defaulted in the same `PropertyGroup` to a known-good develop build for local development. The workflow passes `-p:TerminalGuiVersion=${{ env.TG_VERSION }}` so the build pulls exactly the TG version named by the dispatch — no version drift between the package label and what's linked.

**Why:** The previous "pin develop, replace with release tag at v0.5" plan put the release schedule in tension with TG's own. Mirroring TG's develop story directly removes that tension; consumers who want stability stay on stable, consumers who want early bits opt in. Publishing the same version string TG uses keeps the 1:1 promise from §5.6 honest in both directions.

**How to apply:** Spec §5.1, §5.4, §5.5, §5.6, §7 v0.5 row, and §8 risks all updated in the same PR. The §8 develop-pin risk row is **resolved**; a new "develop publish volume" row is added in its place. Failure handling distinguishes channel: release failures page, develop failures don't (next develop supersedes within hours; spam-paging on every flake would be untenable).

**Status.** Active. Pending TG-side work: a `notify-clet.yml` workflow on `gui-cs/Terminal.Gui` that fires both dispatches with a `CLET_DISPATCH_PAT` (tracked as a separate TG-side issue).

**Pointers.** Spec §5.1, §5.4, §5.5, §5.6, §7, §8. `src/Clet/Clet.csproj` (`<TerminalGuiVersion>` + variable PackageReference). `.github/workflows/release-on-tg-release.yml` (renamed to `release-on-tg.yml`).

## D-019: Distribute clet as a single-project `dotnet tool` (mdv pattern) (Active)

**Context.** Spec §5.4 originally hand-waved at "the `Clet.Tool` project (which references `Clet` and packages the build output as a global tool)" — but no such project exists in the repo, and there is no need for one. The sibling [`gui-cs/mdv`](https://github.com/gui-cs/mdv) viewer ships as a single-csproj global tool: `<PackAsTool>true</PackAsTool>` + `<ToolCommandName>mdv</ToolCommandName>` + `<PackageId>Terminal.Gui.mdv</PackageId>` directly on the executable's csproj. Install command is `dotnet tool install -g Terminal.Gui.mdv`. clet should adopt the same pattern: a single csproj that produces both the AOT single-file binary (for Homebrew/WinGet) and a `dotnet tool` package (for the cross-platform `dotnet tool install` path).

**Decision.** Add `PackAsTool`, `ToolCommandName=clet`, and `PackageId=Terminal.Gui.clet` directly to `src/Clet/Clet.csproj`. Pack the README and LICENSE into the NuGet package via `<None Include="..." Pack="true" PackagePath="/" />`. No separate `Clet.Tool` project. The AOT binary continues to be produced by `dotnet publish -c Release` against the same csproj — `PackAsTool` only affects `dotnet pack` output, not `dotnet publish`. End users on any platform with the .NET SDK can install via `dotnet tool install -g Terminal.Gui.clet` and invoke `clet` from PATH. The package id is namespaced under `Terminal.Gui.` to match the mdv precedent and to make the gui-cs origin obvious in NuGet search.

**Why:** mdv has already proven the single-csproj approach works for a Terminal.Gui-based tool, and a separate `Clet.Tool` project would add a layer of indirection (project reference, build orchestration, second csproj to keep in sync) that earns nothing. The spec's earlier "Clet.Tool project" phrasing was aspirational — never built.

**How to apply:** §5.4 ".NET tool (NuGet)" describes this packaging in concrete terms (properties to set, exact `dotnet pack` / `dotnet tool install` commands). README install hint is updated to `dotnet tool install -g Terminal.Gui.clet`. v0.5 milestone exit criterion (§7) requires `dotnet pack` + local `dotnet tool install` to work end-to-end before the channels-live exit criteria for v1.0 GA.

**Pointers.** Spec §5.4, §7 v0.5 row, §10 step 10. README "Install" section. The `mdv.csproj` reference: <https://github.com/gui-cs/mdv/blob/main/mdv.csproj>.

## D-018: ASCII logo wired into `--help` banner and README hero section (Active)

**Context.** [Issue #12 (branding)](https://github.com/gui-cs/clet/issues/12) approved the three-line box-drawing logo and tagline "One binary. Every prompt. JSON out. Go home." and called for the logo to be wired into `clet --help` and the README hero section.

**Decision.** The ASCII logo is prepended to the Markdown-rendered `--help` output (embedded in `src/Clet/Help/overview.md`), before the tagline/description and usage block. The README `## Press Release` heading is preceded by a full hero section: hero image, code-block logo, tagline, install commands, comparison table, and usage examples (human + AI agent). Spec §4.7 updated to document the `--help` banner format. The logo is also the canonical visual identity for all documentation.

**Status.** Active. Logo, tagline, and install commands are locked as of this PR. GIF/asciinema demo placeholder lands in the README; actual recording defers to v0.3 (issue #3).

**Pointers.** `src/Clet/Help/overview.md` (logo in Markdown template), `README.md` (hero section), `specs/clet-spec.md` §4.7.

## D-017: Link safety default is SurfaceOnly for `clet md` (Active)

**Context.** Spec Appendix A defines a `SurfaceOnly` link policy: hyperlinks are displayed but never opened automatically. The mdv reference viewer already implements this pattern — `LinkClicked` shows the URL in the status bar and sets `e.Handled = true`. AI agents need predictable, safe behavior when running `clet md` on untrusted Markdown.

**Decision.** Default link behavior is SurfaceOnly: clicking a link shows the URL in the status bar, nothing more. A future `--allow-link-open` clet option can opt in to opening links in the default browser. Not implemented at v0.5 — the safe default ships first.

**Status.** Active.

**Pointers.** `src/Clet/Clets/Viewer/MarkdownClet.cs` (LinkClicked handler), spec Appendix A.

## D-016: Help rendering uses print mode, not interactive fullscreen (Active)

**Context.** Spec §4.7 says help is "surfaced through the same dismissable, themed, scrollable viewer experience" as `clet md`. Taken literally, `clet --help` would open an interactive fullscreen TUI that blocks until the user presses `q`. This conflicts with CLI conventions: help must work in pipes (`clet --help | less`), must not block for user interaction, and AI agents read stdout non-interactively.

**Decision.** `clet --help` and `clet help <alias>` render Markdown to ANSI escape sequences and write to stdout, then exit immediately. "Same code path" means the same `Markdown` rendering engine (Terminal.Gui's `Markdown` View with `TextMateSyntaxHighlighter`), not the same interactive fullscreen mode. The print-mode pipeline is adapted from mdv's `RenderMarkdown()`. Root help reads from an embedded `overview.md` resource; per-alias help is generated dynamically from `IClet` metadata.

**Why:** CLI help must be non-interactive for pipes, redirection, and AI agent consumption.

**How to apply:** Any future help-related changes should keep the print-mode pipeline. If interactive help browsing is desired, it should be a separate command (e.g., `clet browse-help`), not the default for `--help`.

**Status.** Active. Spec §4.7 should be read as "same rendering engine" rather than "same interactive mode."

**Pointers.** `src/Clet/Hosting/MarkdownHelpRenderer.cs`, `src/Clet/Hosting/CommandLineRoot.cs` (WriteRootHelp, WriteAliasHelp), `src/Clet/Help/overview.md`.

## D-015: `clet md` content source is file arguments + stdin at v0.5 (Active)

**Context.** Spec §9 open question #4 asks whether `clet md` takes file arguments (`clet md README.md`), stdin (`cat README.md | clet md`), or both.

**Decision.** Both, with the following precedence:
1. File arguments in `options.Arguments` — treated as file paths or glob patterns, expanded and read.
2. Inline content via `--initial` — rendered directly as Markdown text.
3. Stdin if redirected (`Console.IsInputRedirected`) — read to end and render.
4. If none, return `Error("io", "No file specified.")`.

The file expansion logic (glob support, file-not-found warnings) is adapted from mdv's `ExpandFiles()`. Multi-file support uses a `DropDownList` in the status bar, also adapted from mdv.

**Why:** Both input methods are expected by shell users and AI agents. File args are the primary use case; stdin enables piping.

**How to apply:** This resolves spec §9 question #4. The content resolution precedence order is fixed for v1.0.

**Status.** Active. Resolves spec §9 open question #4.

**Pointers.** `src/Clet/Clets/Viewer/MarkdownClet.cs` (content resolution logic).

## D-014: `--title` is a built-in CLI flag, not a per-clet option (Active)

**Context.** Every input clet renders its `RunnableWrapper`/`OpenDialog` with a `Title` and falls back to a per-clet default ("Select an option…", "Enter a number…", etc.). All 14 clets honor `CletRunOptions.Title` if set. The CLI parser, however, had no way to populate it — `--title` was being routed into the per-clet `--<opt>` bucket where most clets ignored it.

**Decision.** `--title <text>` is parsed at the host level (`CommandLineRoot.DispatchAlias`) alongside `--initial`, `--json`, `--timeout`, `--fullscreen`, and stored as `CletRunOptions.Title`. Individual clets do **not** declare `title` in their `Options` list — adding it 14 times would be churn and the per-clet help would falsely imply each clet handles it differently.

**Status.** Active. Listed in root help (§4.7).

**Pointers.** `src/Clet/Hosting/CommandLineRoot.cs` (`DispatchAlias` parsing + `WriteRootHelp`), `src/Clet/Abstractions/CletRunOptions.cs` (Title property), each clet's `Title = options.Title ?? "default"` line.

## D-013: All clet wrappers/dialogs render with `Schemes.Base` (Active)

**Context.** By default a `RunnableWrapper` inherits the surrounding scheme; an `OpenDialog` calls `FileDialog.SetStyle()` which forces `SchemeName` to `Schemes.Dialog` once the dialog enters its running state — even if we set `Schemes.Base` in the object initializer. Without intervention, file/directory pickers render with a different palette than the other 12 inline clets.

**Decision.** All clets set `SchemeName = CletStyling.BaseSchemeName` (resolves `SchemeManager.SchemesToSchemeName(Schemes.Base)`) on their wrapper/dialog. For `pick-file` and `pick-directory`, an `IsRunningChanged` handler re-applies `Schemes.Base` once the dialog has actually started running, working around `FileDialog.SetStyle()`'s `Base → Dialog` rewrite. The handler is **load-bearing** — deleting it silently regresses the file pickers to the dialog scheme.

**Status.** Active. Revisit when Terminal.Gui exposes a way to opt out of `FileDialog.SetStyle()`'s scheme rewrite, at which point the handler can be replaced with the simpler initializer-only form.

**Pointers.** `src/Clet/Hosting/CletStyling.cs`, `src/Clet/Clets/Input/PickFileClet.cs` (IsRunningChanged handler), `src/Clet/Clets/Input/PickDirectoryClet.cs` (same).

## D-012: Code signing deferred post-1.0 (Active)

**Context.** Spec §5.2 calls for macOS (Developer ID + notarization) and Windows (Authenticode) code signing in the release pipeline. Apple Developer Program costs $99/yr; Azure Trusted Signing costs ~$10/mo. Homebrew bottles require signed binaries for Gatekeeper; unsigned binaries get quarantined.

**Decision.** Defer all code signing until after v1.0, when adoption numbers justify the cost. At v0.5/v1.0:
- Homebrew ships a **build-from-source formula** (no bottles, no signing needed; user's machine compiles via `dotnet publish`).
- `dotnet tool install -g clet` works without signing (NuGet packages aren't gated by OS code signing).
- WinGet can ship unsigned `.exe` with a SmartScreen warning; acceptable for early adopters.
- Skip `scripts/sign-macos.sh` and `scripts/sign-windows.ps1` steps in release workflows.

Revisit when download numbers show users hitting Gatekeeper/SmartScreen friction, or when a corporate adopter requires signed binaries.

**Status.** Active. Only three secrets needed at v0.5: `CLET_DISPATCH_PAT`, `NUGET_API_KEY`, `HOMEBREW_TAP_TOKEN`.

**Pointers.** Spec §5.2 (build matrix signing steps), §5.4 (publish steps). `gui-cs/homebrew-tap` repo (must be created before v0.5).

## D-011: `range` is integer-only at v0.3 (Active)

**Context.** Spec §4.3.2 defines the `range` value shape as `{"low": <T>, "high": <T>}` where `<T>` is the scalar of the underlying numeric/date/time type.

**Decision.** At v0.3, `T = int` only. The `RangeClet` uses two `NumericUpDown<int>` controls. Decimal range and date/time range are deferred until demand exists — adding them later is a new clet alias or a generic type parameter, not a breaking change to the existing wire format.

**Status.** Active. Revisit if users request decimal or date ranges before v0.5 schema-lock.

**Pointers.** `src/Clet/Clets/Input/RangeClet.cs`, `src/Clet/Clets/Input/RangeView.cs`.

## D-010: `pick-file`/`pick-directory` run inline, not fullscreen (Active)

**Context.** Spec implies file dialogs need fullscreen because `OpenDialog` is a TG `Dialog`. Early draft plan proposed adding a `RequiresFullscreen` property to `IClet`.

**Decision.** File picker clets render inline by default with `Width = Dim.Fill, Height = Dim.Fill`. Users who want fullscreen pass `--fullscreen` (already handled by `AliasDispatcher` via `options.Fullscreen`). No `RequiresFullscreen` property added to `IClet` — the existing `--fullscreen` flag is sufficient.

**Status.** Active.

**Pointers.** `src/Clet/Clets/Input/PickFileClet.cs`, `src/Clet/Clets/Input/PickDirectoryClet.cs`, `src/Clet/Hosting/AliasDispatcher.cs` (line 39 already checks `options.Fullscreen`).

## D-009: `multi-select` returns array of strings, not indices (Active)

**Context.** Spec §4.3.2 says `multi-select` value shape is `array of integers (zero-based indices, ascending)`.

**Decision.** Return array of selected label texts as a `JsonArray` of strings, not indices. Same reasoning as D-008: shell scripts and AI agents almost always want the label, not the index. Consistent with `select` returning text.

**Status.** Active. Locked at v0.5 schema-lock.

**Pointers.** `src/Clet/Clets/Input/MultiSelectClet.cs`.

## D-008: `select` returns text, not zero-based index (Active)

**Context.** Spec §4.3.2 says `select` value shape is `integer` (zero-based index of the selected item).

**Decision.** The v0.1 implementation returns the selected label text (`string?`), not the index. Rationale: shell scripts and AI agents almost always want the label, not the index. The index is recoverable from the choices list if needed. This is the shipped behavior since v0.1.

**Status.** Active. Locked at v0.5 schema-lock.

**Pointers.** `src/Clet/Clets/Input/SelectClet.cs`.

## D-007: TUIcast keystroke smoke deferred from v0.11 to v0.3 (Active)

**Context.** Spec §5.3 / §6.3 specify TUIcast (PTY-based, deterministic-script keystroke driver) as the process-level smoke harness. Issue #9 (v0.11) initially listed all six smoke cases including a `clet select --json` happy-path that requires an `Enter` keystroke through a PTY.

**Decision.** Land five of the six smoke cases at v0.11 using `Process.Start` (no PTY: `--version`, `--help`, `list --json`, `help select`, `help <unknown>`). Defer the keystroke-driven cases (happy-path Enter, `--timeout 100ms` cancel envelope) to v0.3, where 13 more clets land at the same time and TUIcast pays for its dependency cost. The cancel/timeout *behavior* is unit-tested at v0.11 (`OutputFormatterTests`, `CommandLineRootTests`, `ExitCodesTests`); only the process-level wiring is deferred.

**Status.** Active. `tests/Clet.SmokeTests/scripts/select.txt` placeholder is in place so the v0.3 wire-up is a content edit, not a layout change. Spec §5.3 / §6.3 still describe the full TUIcast harness — that's the v0.3 target, not v0.11 reality.

**Pointers.** [Issue #9](https://github.com/gui-cs/clet/issues/9), `tests/Clet.SmokeTests/CletSmokeTests.cs` (the deliberately `[Fact(Skip=...)]` test).

## D-006: Hand-rolled CLI parser at v0.11 instead of System.CommandLine (Active)

**Context.** Spec §4.6 names `System.CommandLine` (SCL) as the CLI root. SCL is in long-running beta and its API has churned across `2.0.0-beta4`, `beta5`, `beta6` (2022–2025).

**Decision.** Hand-roll a ~300-line parser at v0.11 (`src/Clet/Hosting/CommandLineRoot.cs`). The v0.11 surface is small: `--help`, `--version`, `help <alias>`, `list [--json]`, `<alias> [initial] [--json] [--timeout] [--<opt> <value>]`. The dependency churn isn't worth it for this surface size.

**Status.** Active for v0.11. Revisit at **v0.3** if AOT polish needs SCL's reflection-free parsing or if the surface grows enough (more clets, more global flags) that hand-rolled stops being clean. If we swap, the call site is one constructor in `Program.Main`.

**Pointers.** `src/Clet/Hosting/CommandLineRoot.cs`. Spec §4.6 still names SCL — that's intent, not current code.

## D-005: Non-generic `IClet.RunBoxedAsync` via default interface methods (Active)

**Context.** The host needs to dispatch any clet without knowing its `TValue` at compile time, but reflection-based generic dispatch isn't AOT-clean (and AOT lands at v0.3).

**Decision.** Add a non-generic `Task<BoxedCletResult> RunBoxedAsync(...)` to `IClet`. Provide it as a default interface method on `IClet<T>` and `IViewerClet` that wraps the typed `RunAsync`. Concrete clets (`SelectClet`) get it for free; the host calls through `IClet`.

**Status.** Active. `BoxedCletResult` is `(CletRunStatus, object? Value, string? ErrorCode, string? ErrorMessage)`.

**Pointers.** `src/Clet/Abstractions/IClet.cs`, `src/Clet/Abstractions/BoxedCletResult.cs`, `src/Clet/Abstractions/IViewerClet.cs`.

## D-004: Source generator deferred — `BuiltInClets.RegisterAll` hand-written (Superseded by D-021)

**Context.** Spec §4.4 specifies a Roslyn source generator (`src/Clet.SourceGen`) that emits `BuiltInClets.RegisterAll(ICletRegistry)` from `[Clet("alias", typeof(TResult))]` attributes. The generator project landed in v0.1 as a placeholder; the actual generator is not implemented.

**Decision.** Hand-write `Registry/BuiltInClets.cs` for now. As clets are added (v0.3 wave), keep registering them manually until the generator earns its keep. Bar-raise critique #11 questioned whether the generator is worth its complexity at all.

**Status.** Superseded by [D-021](#d-021-auto-discovered-clets-any-ivaluet-view-just-works-deferred-to-v2-active). The "revisit before v0.3 GA" trigger fired with the answer: don't bother in v1.x. The auto-discovery question is broader than just the source generator; D-021 captures the full design exploration and the deferral to v2.

**Pointers.** `src/Clet/Registry/BuiltInClets.cs`, `src/Clet.SourceGen/Placeholder.cs`. Bar-raise [#BR-11 in the bar-raise backlog issue](https://github.com/gui-cs/clet/issues/11) ticked.

## D-003: `range` clet emits `{"low": <T>, "high": <T>}` (Active)

**Context.** Spec §9 originally listed the `range` `value` shape as an open question (tuple vs object vs separate fields).

**Decision.** Named object: `{"low": <T>, "high": <T>}` where `<T>` matches the range's underlying numeric/date/time type. Stable JSON across languages, self-documenting, easy to extend later (e.g., add `step`).

**Status.** Active. Locked at v0.5 schema-lock per spec §4.3.2.

**Pointers.** `specs/clet-spec.md` §4.3.2.

## D-002: Cancel envelope is `{"schemaVersion":1,"status":"cancelled"}` regardless of TG behavior (Active)

**Context.** TG #5157 (now landed on develop) leaves "disposition of `IValue<T>.Value` after cancel" as a TG-internal decision. Clet's wire contract was at risk of being coupled to that decision.

**Decision.** Decouple. On cancel, clet emits exactly `{"schemaVersion":1,"status":"cancelled"}` — no `value`, no `code`, no partial result — regardless of whether the underlying View's `IValue<T>.Value` is readable. TG's eventual answer is welcome but not load-bearing.

**Status.** Active. Locked in spec §3.1 and §4.3.

**Pointers.** `specs/clet-spec.md` §3.1, §4.3. `src/Clet/Json/SchemaV1.cs` `Cancelled()` factory.

## D-001: No `type` field on the JSON wire envelope (Active)

**Context.** Press release originally showed `{"status":"ok","type":"System.String","value":"prod"}`. CLR-typed `type` field leaked .NET into a cross-language wire format that AI agents in any language consume.

**Decision.** Drop `type` from the result envelope entirely. Result types are advertised once per alias by `clet list --json`; consumers cache the registry once per session and don't branch on per-call type names.

**Status.** Active. Locked at schema v1 (spec §4.3, §4.3.1).

**Pointers.** `specs/clet-spec.md` §4.3, §4.3.1. `README.md` lines 70 and 80 (envelope examples). `src/Clet/Json/SchemaV1.cs`.
