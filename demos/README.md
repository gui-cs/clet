# Demo Scripts

Example scripts showing how to chain clets together in real workflows. Each script uses `dotnet run` against the local build — no global tool install needed.

## Scripts

### `file-then-md` — Pick a file, then view it

Pick a Markdown file using the `file` clet's tree dialog, then open it in the `md` viewer.

Available in PowerShell (`.ps1`) and bash (`.sh`).

```sh
# PowerShell
./demos/file-then-md.ps1

# bash (requires jq)
./demos/file-then-md.sh

# Start in a specific directory
./demos/file-then-md.ps1 ~/projects
./demos/file-then-md.sh ~/projects
```

**What it demonstrates:**

- Chaining an input clet (`file`) into a viewer clet (`md`)
- Using `--json` + `--output` to capture structured results without interfering with TUI rendering
- Parsing the JSON envelope to extract the `value` field
- Handling cancellation (Esc exits with code 130)

### The `--output` pattern

Terminal.Gui renders to stdout, so shell command substitution (`$(clet ...)` or `$result = clet ...`) swallows the TUI. The `--output <path>` flag writes the result to a file instead, keeping stdout free for the TUI:

```sh
# PowerShell
$tmp = [System.IO.Path]::GetTempFileName()
clet file --json --output $tmp
$result = Get-Content $tmp -Raw | ConvertFrom-Json
# use $result.value ...
```

```sh
# bash
tmp=$(mktemp)
clet file --json --output "$tmp"
file=$(jq -r '.value' "$tmp")
# use $file ...
```

## Prerequisites

- .NET 10.0 preview SDK
- PowerShell 7+ (for `.ps1` scripts)
- `jq` (for `.sh` scripts only)

## Adding new demos

Keep each demo focused on one workflow. Provide both `.ps1` and `.sh` variants where practical. Use `dotnet run --project` relative to `$PSScriptRoot` / `$script_dir` so scripts work from any working directory.
