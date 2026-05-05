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

**Threat:** An attacker passes terminal escape sequences (C0/C1 control codes) in `--title`, `--initial`, or positional arguments to manipulate the terminal state (cursor repositioning, window title injection, clipboard access via OSC 52).

**Mitigation:** Terminal.Gui's View layer renders text through its own attribute/cell model, not by passing raw strings to the terminal. User-supplied strings become `Text` properties on Views (`TextField`, `OptionSelector`, etc.), which are rendered cell-by-cell with explicit attributes. Control characters in user strings are displayed as glyphs (or ignored), not interpreted as escape sequences.

**For JSON output (`--json`):** The `OutputFormatter` writes to stdout via `SchemaV1.ToJson()`, which uses `System.Text.Json` source-generated serialization. JSON string escaping handles control characters per RFC 8259 (e.g. `\u001b` for ESC). No raw user input reaches stdout unescaped.

**For plain-text output:** `OutputFormatter.Write` calls `stdout.WriteLine(value)` for the result value. If the value contains terminal escapes, they pass through to the terminal. This is acceptable because: (a) the value is the *result* of user interaction, not attacker-controlled input, and (b) `--json` mode is the recommended machine-readable path.

**Verification (v0.9):** The Appendix A claim "all output to stdout/stderr passes through a terminal-escape filter" is refined here. clet does not implement a standalone C0/C1 stripping filter on output paths. Instead, sanitization is architectural: TG renders user strings through its cell model (never raw), and JSON mode uses RFC 8259 escaping. Plain-text mode passes through the interaction result as-is, which is acceptable per the rationale above. The Appendix A wording is updated to match this reality.

### `--title` sanitization

**Threat:** `--title` sets the TG window/border title. If the string contained escape sequences that leaked to the terminal, it could set the terminal window title (OSC 0/2) or worse.

**Mitigation:** TG's `Border` rendering draws the title string cell-by-cell through the driver's attribute model. The title string is never written raw to the terminal output stream. This is inherent to TG's architecture, not a clet-specific defense.

### Markdown link policy

**Threat:** A Markdown file rendered by `clet md` contains a link that, if followed, opens a browser to a malicious URL, exfiltrates data via URL parameters, or triggers an OS handler for a custom scheme (`file://`, `ssh://`, etc.).

**Mitigation:** Default link policy is `SurfaceOnly` (D-017):
- `LinkClicked` event handler shows the URL in the status bar and sets `e.Handled = true`.
- No link is ever opened automatically.
- AI agents running `clet md` on untrusted content are safe by default.

**Future opt-in (deferred):** A `--allow-link-open` clet option is planned to allow opening links in the default browser. It is not implemented in v1.0. The safe default ships first; the opt-in will be gated behind an explicit user flag when added.

### File access scope

**Threat:** `clet pick-file` or `clet pick-directory` could be used to navigate to sensitive directories. `clet md` could be pointed at sensitive files.

**Mitigation:**
- File pickers use TG's `OpenDialog`, which honors OS-level file permissions and sandboxing.
- `clet md` reads files via `File.ReadAllText()` — standard OS permission checks apply.
- No privilege escalation: clet runs as the invoking user, never setuid/setgid.
- The `--root` option on `pick-file`/`pick-directory` constrains the starting directory but does not prevent navigation outside it (that's the OS sandbox's job). `--root` is a UX convenience, not a security boundary.

**Verification (v0.9):** `pick-file` and `pick-directory` delegate entirely to TG's `OpenDialog`/`SaveDialog`. No custom path traversal logic exists in clet. Path values like `--root ../../../../etc` resolve via the OS filesystem — clet does not perform its own path validation beyond what TG and the OS provide. This is the intended design: clet trusts the OS permission model.

### Plugin loading exclusion

**Threat:** Dynamic assembly loading (`Assembly.LoadFrom`, `Assembly.Load`) could execute arbitrary code from untrusted DLLs.

**Mitigation:** clet v1.0 does not load plugins, assemblies, or any user-supplied code. The clet registry (`BuiltInClets.RegisterAll`) is statically compiled. There is no extension point, no plugin directory, no `--load-assembly` flag. This is a deliberate v1.0 constraint (spec §2 out-of-scope).

NativeAOT publishing (`PublishAot=true`) further closes this surface: AOT binaries cannot load managed assemblies at runtime.

**Verification (v0.9):** Confirmed — no calls to `Assembly.LoadFrom`, `Assembly.Load`, or related reflection-based assembly loading APIs exist in the `src/Clet/` codebase. This row is closed.

### Denial of service

**Threat:** Large files passed to `clet md`, extremely long `--initial` values, or high-frequency invocations.

**Mitigation:**
- **Input-size caps (v0.9):** `--initial` is capped at 64 KiB. `clet md` caps stdin and file content at 8 MiB. Exceeding either cap exits with code 65 (`"input-too-large"`). This prevents an agent piping unbounded data from OOMing the process.
- `--timeout` flag enables callers to set an upper bound on execution time.
- No network I/O, no database, no shared state between invocations. Each `clet` invocation is a short-lived process.

### JSON output integrity

**Threat:** Consumers relying on `--json` output could be confused by malformed or unexpected JSON.

**Mitigation:**
- `SchemaV1` is the single serialization path for all JSON output.
- Source-generated `CletJsonContext` ensures AOT-safe, deterministic serialization.
- Contract tests (unit + smoke) validate every envelope shape: `ok`, `cancelled`, `error`, `no-result`.
- `schemaVersion: 1` is locked for clet 1.x; additive-only changes per §4.3.1.

## Out of scope for v1.0

- **Network access:** clet makes no network calls. No telemetry, no update checks, no remote content fetching.
- **Code signing verification:** clet does not verify its own signature at runtime. Code signing (D-012) is deferred post-1.0.
- **Multi-user / privilege separation:** clet is a single-user CLI tool. No daemon mode, no IPC, no shared state.
