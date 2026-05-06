# clet Threat Model

> **Status:** Published at v0.5. Expands the sketch in `specs/clet-spec.md` Appendix A.

## Scope

This document covers the attack surface of the `clet` CLI binary (`src/Clet/`) as shipped in v1.0. It does not cover the Terminal.Gui library itself (that's TG's responsibility), CI/CD pipeline security (covered in `docs/runbooks/release-rollback.md`), or post-v1.0 plugin loading.

## Trust boundaries

```
┌─────────────────────────────────────────────┐
│ Shell / AI Agent (untrusted)                │
│   args, stdin, env vars, file paths         │
├─────────────────────────────────────────────┤
│ clet CLI host (CommandLineRoot, Dispatcher) │
│   parse, validate, sanitize                 │
├─────────────────────────────────────────────┤
│ Terminal.Gui (trusted library)              │
│   View rendering, input handling            │
├─────────────────────────────────────────────┤
│ Terminal emulator (trusted)                 │
│   ANSI rendering, PTY I/O                   │
└─────────────────────────────────────────────┘
```

The trust boundary is between the shell/agent layer and the clet CLI host. Everything above that line is untrusted input.

## Untrusted inputs

| Input | Entry point | Threat |
|-------|-------------|--------|
| `--initial <value>` | `CommandLineRoot.DispatchAlias` | Injection into View text fields |
| `--title <text>` | `CommandLineRoot.DispatchAlias` | Terminal escape injection via window title |
| `--<opt> <value>` | `CommandLineRoot.DispatchAlias` | Per-clet option values (e.g. `--options` for select) |
| Positional arguments | `CletRunOptions.Arguments` | File paths (path traversal), label text |
| stdin | `Console.In.ReadToEnd()` | Arbitrary content for `clet md` |
| File content | `File.ReadAllText()` in `MarkdownClet` | Malicious Markdown (script injection, link abuse) |
| Environment variables | `ConfigurationManager` (TG) | Theme/config override |

## Mitigations

### Terminal escape sanitization

**Threat:** An attacker passes terminal escape sequences (C0/C1 control codes) in `--title`, `--initial`, positional arguments, or markdown content (via `clet md`) to manipulate the terminal state (cursor repositioning, window title injection, clipboard access via OSC 52, hyperlink spoofing via OSC 8).

**Mitigation:** clet implements a defense-in-depth approach:

1. **Input sanitization (TerminalEscapeSanitizer):** All user-supplied content is stripped of dangerous terminal control sequences *before* it reaches Terminal.Gui. The sanitizer removes ESC (`\x1b`), BEL (`\x07`), 8-bit CSI (`\x9b`), 8-bit OSC (`\x9d`), and C1 7-bit pairs (`\x1b@` through `\x1b_`). This filter runs at every code path that hands user content to the renderer:
   - `MarkdownClet`: inline content and file-loaded content are sanitized before assignment to `markdownView.Text`.
   - `MarkdownHelpRenderer.RenderToAnsi`: input markdown is sanitized before rendering, and a second pass on the rendered ANSI output strips any user-payload escape sequences that survived through TG rendering (while preserving the renderer's own SGR/cursor sequences).

2. **TG's cell model (defense-in-depth):** Terminal.Gui's View layer renders text through its own attribute/cell model. User-supplied strings become `Text` properties on Views, which are rendered cell-by-cell with explicit attributes. However, clet does **not** rely on TG to filter terminal escapes — the `TerminalEscapeSanitizer` is clet's own defense, applied before content reaches TG (see D-030).

**For JSON output (`--json`):** The `OutputFormatter` writes to stdout via `SchemaV1.ToJson()`, which uses `System.Text.Json` source-generated serialization. JSON string escaping handles control characters per RFC 8259 (e.g. `\u001b` for ESC). No raw user input reaches stdout unescaped.

**For plain-text output:** `OutputFormatter.Write` calls `stdout.WriteLine(value)` for the result value. If the value contains terminal escapes, they pass through to the terminal. This is acceptable because: (a) the value is the *result* of user interaction, not attacker-controlled input, and (b) `--json` mode is the recommended machine-readable path.

### `--title` sanitization

**Threat:** `--title` sets the TG window/border title. If the string contained escape sequences that leaked to the terminal, it could set the terminal window title (OSC 0/2) or worse.

**Mitigation:** TG's `Border` rendering draws the title string cell-by-cell through the driver's attribute model. The title string is never written raw to the terminal output stream. This is inherent to TG's architecture, not a clet-specific defense.

### Markdown link policy

**Threat:** A Markdown file rendered by `clet md` contains a link that, if followed, opens a browser to a malicious URL, exfiltrates data via URL parameters, or triggers an OS handler for a custom scheme (`file://`, `ssh://`, etc.).

**Mitigation:** Default link policy is `SurfaceOnly` (D-017):
- `LinkClicked` event handler shows the URL in the status bar and sets `e.Handled = true`.
- No link is ever opened automatically.
- A future `--allow-link-open` option can opt in to opening links; it is off by default.
- AI agents running `clet md` on untrusted content are safe by default.

### File access scope

**Threat:** `clet pick-file` or `clet pick-directory` could be used to navigate to sensitive directories. `clet md` could be pointed at sensitive files.

**Mitigation:**
- File pickers use TG's `OpenDialog`, which honors OS-level file permissions and sandboxing.
- `clet md` reads files via `File.ReadAllText()` — standard OS permission checks apply.
- No privilege escalation: clet runs as the invoking user, never setuid/setgid.
- The `--root` option on `pick-file`/`pick-directory` constrains the starting directory but does not prevent navigation outside it (that's the OS sandbox's job).

### Plugin loading exclusion

**Threat:** Dynamic assembly loading (`Assembly.LoadFrom`, `Assembly.Load`) could execute arbitrary code from untrusted DLLs.

**Mitigation:** clet v1.0 does not load plugins, assemblies, or any user-supplied code. The clet registry (`BuiltInClets.RegisterAll`) is statically compiled. There is no extension point, no plugin directory, no `--load-assembly` flag. This is a deliberate v1.0 constraint (spec §2 out-of-scope).

NativeAOT publishing (`PublishAot=true`) further closes this surface: AOT binaries cannot load managed assemblies at runtime.

### Denial of service

**Threat:** Large files passed to `clet md`, extremely long `--initial` values, or high-frequency invocations.

**Mitigation:**
- `--timeout` flag enables callers to set an upper bound on execution time.
- `clet md` reads files with `File.ReadAllText()` — bounded by available memory, same as any CLI tool.
- No network I/O, no database, no shared state between invocations. Each `clet` invocation is a short-lived process.

### JSON output integrity

**Threat:** Consumers relying on `--json` output could be confused by malformed or unexpected JSON.

**Mitigation:**
- `SchemaV1` is the single serialization path for all JSON output.
- Source-generated `CletJsonContext` ensures AOT-safe, deterministic serialization.
- Contract tests (unit + smoke) validate every envelope shape: `ok`, `cancelled`, `error`, `no-result`.
- `schemaVersion: 1` is locked for clet 1.x; additive-only changes per §4.3.1.

## Release pipeline

### Script injection via workflow inputs

**Threat:** GitHub Actions interpolates `${{ github.event.* }}` expressions directly into `run:` scripts *before* the shell parses them. An attacker who controls a `repository_dispatch` payload (e.g. a compromised Terminal.Gui PAT) or a `workflow_dispatch` input (e.g. a compromised maintainer account) can inject arbitrary shell commands. The `resolve-version` job runs with `contents: write` + `issues: write` and the downstream `publish-nuget` job uses `secrets.NUGET_API_KEY`, making successful injection high-impact (backdoored NuGet package, exfiltrated secrets).

**Mitigation (D-029):** All user-controlled expressions (`github.event.client_payload.tg_version`, `github.event.inputs.tg_version`, `github.event.inputs.version_override`) are bound to step-level `env:` variables and only referenced as `$VAR` inside the `run:` script — the standard GitHub hardening pattern. Additionally, both inputs are validated against a strict allowlist regex (`^[0-9A-Za-z._+*-]+$`) before use; the step exits 2 if validation fails. See [GitHub's hardening guide](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions#good-practices-for-mitigating-script-injection-attacks).

**What is not user-controlled:** `github.event_name` is runner-set metadata (not attacker-controllable), but it is also moved into `env:` for defense-in-depth.

## Out of scope for v1.0

- **Network access:** clet makes no network calls. No telemetry, no update checks, no remote content fetching.
- **Code signing verification:** clet does not verify its own signature at runtime. Code signing (D-012) is deferred post-1.0.
- **Multi-user / privilege separation:** clet is a single-user CLI tool. No daemon mode, no IPC, no shared state.
