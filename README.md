## Press Release

### Terminal.Gui launches `clet`: typed terminal prompts (and viewers) for shells, scripts, and AI agents

**One CLI for every Terminal.Gui View. Full keyboard and mouse, themed colors, consistent navigation, JSON output, predictable exit codes. Prompt for a value, pick a file from a real tree, render a README with proper formatting, all from the same tool.**

**Durango, CO ; Friday, May 8, 2026.** The Terminal.Gui team today released `clet` 1.0, a command-line tool that turns any Terminal.Gui View into a first-class shell command. A single binary on `PATH` exposes:

- A real file dialog (tree navigation, extension filters, hidden-file toggle, breadcrumbs, mouse).
- Validated text inputs, sliders, ranges, multi-selects, attribute pickers, color pickers, date/time pickers.
- A Markdown viewer (`clet md README.md`) backed by Terminal.Gui v2's `Markdown` View: headings, lists, tables, code blocks, links, all rendered with the same theme as the rest of your terminal toolkit.

`clet` installs natively via Homebrew and WinGet; no .NET runtime is required on the user's machine. The binary is self-sufficient.

What makes `clet` different from existing prompt and viewer tools is what it inherits. Every Terminal.Gui View brings full keyboard and mouse handling, a theme honored through `ConfigurationManager` (the same theme your other Terminal.Gui apps use), accessibility hooks, and the consistency that comes from one navigation model across every command. Pick a file, then read its contents through `clet md`, then confirm an action; same keys, same colors, same conventions, no context switch.

Until now, terminal prompts and viewers have been split across half a dozen tools (`read`, `dialog`, `whiptail`, `fzf`, `gum`, `glow`, `bat`, `less`, plus bespoke per-language libraries), none of which share a result contract, validation model, theme, or rendering style. `clet` is the missing primitive that unifies them.

For inputs, every prompt produces a value of a known type, advertised in `clet help <alias>` and emitted in a stable JSON schema under `--json`. For viewers, the same contract returns `{"status":"ok"}` on dismiss and `{"status":"cancelled"}` on Ctrl-C. Cancel is exit 130 (SIGINT convention); usage error is 2; success is 0. Inline-capable prompts render in the lines they need and restore the cursor; full-screen viewers like `md` claim the alt-screen and return it cleanly when you press `q`.

For AI agents, `clet` is the missing structured-elicitation surface. Agents call `clet pick-file --json --root ./src --timeout 30s` and get back a typed result. They call `clet md ./CHANGELOG.md` to surface release notes to the human in the loop, with proper formatting, and continue when the human dismisses.

For View authors, exposure is one line: implement `IValue<T>` for input clets, or implement the `IViewerClet` lifecycle for viewers, register the alias, ship.

> "Terminal.Gui spent two years building a real UI framework for the terminal: layout, themes, mouse, accessibility, the lot. `clet` is the realization that all of that work was already a CLI tool; it just needed a `Program.Main`. Even the Markdown viewer turned out to be a clet, once we accepted that 'q' is a valid answer to a question."
> ; **Tig Kindel, Terminal.Gui maintainer**

`clet` is available today via:

```
brew install gui-cs/tap/clet      # macOS, Linux
winget install gui-cs.clet        # Windows 10/11
dotnet tool install -g clet       # any platform with .NET SDK
```

---

### Customer voices

**Maya Okonkwo, platform engineer (lives in the terminal).**
> "I write maybe ten one-off bash scripts a week, and half of them need to ask me 'which environment?' or 'confirm, this will delete 40k rows.' I used to mix `read`, `gum`, and `fzf`, and the exit codes never matched. The thing I didn't expect to care about was the file picker. `gum file` is `find | filter`. `clet pick-file` is an actual tree with extension filters and the same keys I already know from every other TG app at work. And when my deploy script wants to show me the changelog before I confirm, `clet md ./CHANGELOG.md` does it in the same theme, with the same keys, and it doesn't dump 800 lines into my scrollback when I quit."

**Claude (Anthropic AI agent, used inside an autonomous coding session).**
> "Before `clet`, when I needed a human decision mid-task ('three files match this rename, which did you mean?'), I had to print a numbered list and parse the human's reply. Numbers, letters, 'the second one' (which was, with discouraging frequency, the third one), typos: every variant was a chance to misread. With `clet pick --json` I get `{"status":"ok","value":"src/User.ts"}` or `{"status":"cancelled"}`. The viewer side matters too: when I want to show a human the diff I'm about to apply, `clet md` renders it properly and I get a clean `{"status":"ok"}` when they've actually seen it. That's better than asking 'are you ready?' and hoping."

**Priya Raghavan, View author (built a custom `DurationPicker`).**
> "I spent a weekend writing `DurationPicker`, a Terminal.Gui View that lets you pick a `TimeSpan` with sane keyboard handling. I wanted teammates to use it from shell scripts too. With `clet` I added one registration call and `clet duration --initial 1h30m --json` works. My View got mouse support, my company's TG theme, and the same Esc-cancel semantics as every other clet, for free."

**Tig Kindel, Terminal.Gui v2 maintainer.**
> "My job is to say no to things that bloat the core. `clet` passed because it adds zero types to `Terminal.Gui.ViewBase`, doesn't touch `IValue<T>`, lives in its own assembly, and uses an instance `ICletRegistry` instead of static singletons. The thing that surprised me was how much it advertised the parts of TG v2 most users don't see: `ConfigurationManager` themes, key remapping, the FileDialog rewrite, and the Markdown View (which is, frankly, one of the best things we shipped in v2 and almost nobody knew about it). People install `clet` for the prompt, then ask whether they can build a whole app with that file dialog. Yes. We've been telling you for two years."

---

## FAQ

### Customer-facing

**Q: Why not just use `gum` (or `glow`, or `bat`, or `dialog`)?**
Each of those is good at one thing. `clet` is the unification, with a real UI toolkit underneath. Five differences that matter:

1. **A real UI toolkit.** Every clet has full mouse support, configurable keybindings (via `ConfigurationManager`), themed colors that match the rest of the user's TG environment, accessibility hooks, and one consistent navigation model. `gum` widgets and `glow` rendering are independent reimplementations; `clet` widgets and viewers are *the same Views* that ship in Terminal.Gui, sharing the same theme and conventions.
2. **A real file dialog.** `gum file` is fuzzy-filter over `find` output. `clet pick-file` is Terminal.Gui's `FileDialog`: tree, sortable columns, extension filters, hidden-file toggle, breadcrumbs, mouse, platform-aware path handling.
3. **Typed result contract.** `gum input` returns a string. `clet` returns a typed value, advertised in `--json` with a schema. Agents and scripts don't reparse.
4. **Pluggable Views.** Anyone who writes a Terminal.Gui View gets a CLI for free. (Third-party clet *distribution* is v2; the registry itself is v1.)
5. **Inputs and viewers in one tool.** Read a Markdown file with `clet md`, then ask a question with `clet confirm`, with the same theme, same keys, same exit codes. You don't carry a separate viewer (`glow`, `bat`) plus a separate prompter (`gum`).

For a shell user who only needs `read`-with-validation, `gum` is fine. We are not competing for that user.

**Q: What's the difference between an input clet and a viewer clet?**
- **Input clets** prompt for a value (`text`, `pick-file`, `select`, etc.). They return a typed result on success: exit 0, `{"schemaVersion":1,"status":"ok","value":...}`. The result type is advertised once per alias by `clet list --json`, not on every result envelope.
- **Viewer clets** render content for the user to read or browse (`md`, and the family that follows). They return on dismiss: exit 0, `{"status":"ok"}`. There is no `value`; the contract is "did the human see it and acknowledge."

Both share theming, keybindings, mouse, exit codes, and the JSON envelope.

**Q: How does an AI agent discover what clets exist?**
`clet list --json` returns the registry: aliases, descriptions, kind (input or viewer), result type for inputs, option schemas. Agents read this once per session and cache it.

**Q: What does the JSON output look like?**
```json
{ "schemaVersion": 1, "status": "ok",        "value": "prod" }                             // input
{ "schemaVersion": 1, "status": "ok" }                                                     // viewer dismiss
{ "schemaVersion": 1, "status": "cancelled" }
{ "schemaVersion": 1, "status": "error",     "code": "validation",   "message": "..." }
```

**Q: Exit codes?**
`0` success; `1` empty/no-result; `2` usage error; `64` through `78` reserved (BSD `sysexits`); `130` cancelled.

**Q: What does "inline" actually mean here?**
For input clets, the prompt renders in the cursor's current position, claims only the lines it needs, and restores the cursor when it exits (like `fzf --height` or `gum input`). For viewer clets, full-screen alt-screen is the default (paging through a long Markdown file inline doesn't help anyone), with `--inline-height N` for short content. If we cannot land true inline rendering for input clets in v1.0, we ship without the inline claim rather than dilute it.

**Q: Cancellation, timeouts, and keybindings?**
By default, Esc and Ctrl-C cancel input clets; `q`, Esc, and Ctrl-C dismiss viewer clets. All keys are remappable through `ConfigurationManager`; `clet` respects whatever the user has set in their TG config. `--timeout <duration>` cancels after the duration. AI agents are expected to set timeouts.

**Q: Which clets ship in v1.0?**
**Input clets (14):**
- `text`, `int`, `decimal`
- `select`, `multi-select`, `confirm`
- `pick-file`, `pick-directory`
- `date`, `time`, `duration`
- `color`, `attribute-picker`
- `range`

**Viewer clets (1):**
- `md` (Markdown, via Terminal.Gui's built-in `Markdown` View)

Notable absence: `password`. We are deliberately not shipping a password clet in v1.0; secret handling deserves its own threat-modeled release.

**Q: Theming?**
Whatever theme is configured in your TG `ConfigurationManager` (system or user) applies to every clet, input and viewer alike. `--theme <name>` overrides per-invocation.

**Q: How is `clet` updated and versioned?**
The `clet` version always matches the Terminal.Gui version it's built against. When TG cuts a release on `main` (say, TG 2.5.0), GitHub Actions in `gui-cs/clet` rebuilds `clet 2.5.0` against the new TG, signs the artifacts, publishes the Homebrew bottle, submits the WinGet manifest, and pushes the .NET tool package. There is no separate `clet` release cadence to track. Users update through their installer's normal channel: `brew upgrade`, `winget upgrade`, or `dotnet tool update -g clet`. Two cadences would have been one cadence too many.

### Engineering

**Q: Do users need .NET installed?**
**No, for `brew install` and `winget install`.** Those channels distribute a NativeAOT-compiled binary; the .NET runtime is not present on disk and not required. The binary is roughly 8MB.
**Yes, for `dotnet tool install -g clet`.** That channel exists for plugin authors and CI scenarios.

**Q: NativeAOT, then?**
Yes for v1.0. Trade accepted: third-party clet *runtime loading* is deferred to v2 (you cannot `Assembly.LoadFrom` into an AOT'd process). The v1.0 clet set, including `md`, is statically linked. View authors with a clet they want shipped open a PR against `gui-cs/clet`.

**Q: Sync or async `IClet`?**
Async. `Task<CletRunResult<T>> RunAsync(IApplication app, string? initial, CletRunOptions options, CancellationToken ct)` for input clets; the viewer counterpart returns `Task<CletRunResult>` (no `T`).

**Q: Static `CletRegistry` or instance `ICletRegistry`?**
Instance. A static convenience façade may exist for the CLI's own bootstrap; it is not the primitive.

**Q: Initial-value parsing?**
`IParsable<TSelf>` from .NET 7. View authors whose result type already implements `IParsable<T>` get `--initial` parsing for free; others register a parser delegate.

**Q: Code signing and notarization?**
Deferred until adoption justifies the cost — see decisions log entry D-012 in `specs/decisions.md`. v1.0 ships unsigned: Homebrew installs build-from-source (no Gatekeeper friction since the user's machine compiles), `dotnet tool install -g clet` works without OS code signing, and WinGet ships an unsigned `.exe` (a one-time SmartScreen warning, acceptable for early adopters). We'll revisit when download numbers show users hitting friction or a corporate adopter requires signed binaries.

**Q: Security model?**
Inputs from `--initial`, env vars, and stdin are untrusted. Terminal-escape sanitization on stderr/stdout output paths we control. `--title` and other display strings sanitized. `clet md` uses the `Markdown` View's link policy (links are surfaced, not auto-opened). Threat model published with v0.5.

**Q: Why "clet"?**
Because `cmdlet` was taken, and the other plausible short forms led to places we did not want our brand to lead. `clet` was the shortest survivor of an unusually thorough naming review. We checked.

**Q: What goes in the v0.5 milestone?**
Naming locked; JSON schema locked; exit-code table locked; inline rendering proven on macOS Terminal, iTerm2, Windows Terminal, GNOME Terminal; v1.0 input and viewer lists locked; `Markdown` View integration verified end-to-end including link safety; threat model published; Homebrew tap and WinGet manifest in working draft form; the gui-cs/clet release workflow proven against a real TG release cut.

### Strategic

**Q: Why does Terminal.Gui own this rather than a separate project?**
The pitch ("every TG View is a CLI command") depends on the registry and the View ecosystem being the same ecosystem. Splitting it means fragmenting attention. `clet` ships as a single binary in its own repo (`gui-cs/clet`) so its native-installer ops stay out of TG's hair, while the View ecosystem it advertises is unchanged TG. In v1.0 the clet abstractions (`IClet`, `ICletRegistry`, `IViewerClet`) are internal to the binary; v2 may extract them into a published `Clet.Abstractions` NuGet once third-party plugin loading is in scope (today, NativeAOT precludes runtime `Assembly.LoadFrom` into the CLI). TG core changes only on the two narrow seams clet needs and any TG app benefits from (#5157, #5158).

**Q: What does success look like 12 months after launch?**
- 1k+ weekly active users (opt-in usage ping).
- 500+ Homebrew installs and 500+ WinGet installs in the first 90 days.
- 3+ AI-agent products integrating `clet list --json` for human-in-the-loop elicitation.
- `clet md` displaces at least one of `glow`/`bat`/`mdcat` in measurable user workflows.
- At least one PR to `gui-cs/clet` adding a third-party-authored clet, accepted into v1.x.
- Zero breaking changes to `IValue<T>` attributable to clet pressure.

**Q: What kills this project?**
- Inline rendering that doesn't actually feel inline across the four target terminals (we'd ship a worse `gum`).
- AI-agent JSON contract churn (consumers stop trusting it).
- Native-installer pipeline that breaks on every release (the auto-publish workflow is the operational core; if it gets flaky, every TG release becomes a clet incident).
- The `Markdown` View losing ground to `glow` on common Markdown variants (we'd ship a worse viewer alongside a fine prompter).
- Three reasonable people independently pointing out, in print, the obvious thing about the name.

We commit to walking away from v1.0 if any of the first four is unresolved at v0.9. The fifth we will simply outlast.
