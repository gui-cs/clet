## Browser mode

By default, `md` runs in browser mode. Click local `.md` links to navigate to
them. Use **Ctrl+Left** / **Ctrl+Right** (or the ← → buttons in the status bar)
to go back and forward. Fragment anchors (`file.md#heading`) scroll to the
matching heading.

Use `--no-browse` to disable browser mode (no link navigation, no back/forward).

## File access

`clet md` restricts which files can be opened to prevent unintentional exposure
of sensitive files. By default only `.md`, `.markdown`, and `.txt` files inside
the current working directory are allowed.

To grant access to a file or directory for the current invocation only, use
`--allow-file`:

```sh
clet md --allow-file /path/to/dir /path/to/dir/README.md
```

To grant permanent access to specific directories, add them to
`FileAccessSettings.AllowedPaths` in `~/.tui/clet.config.json`:

```jsonc
"FileAccessSettings.AllowedPaths": [
  "/home/user/projects",
  "/home/user/docs"
]
```

Files under those directories will be allowed by `clet md` and `clet edit`
regardless of the working directory. Run `clet config` to open the config file.

## Examples

```sh
# View a markdown file (full-screen, dismiss with q/Esc):
clet md ./README.md

# View multiple files (dropdown selector in status bar):
clet md *.md

# Render to stdout without the TUI:
clet md --cat ./CHANGELOG.md

# Pipe markdown from stdin:
echo "# Hello" | clet md

# Change syntax highlighting theme:
clet md --theme Monokai ./README.md

# View a file outside the working directory (ephemeral override):
clet md --allow-file ../other-repo ../other-repo/README.md

# Disable browser mode (no top bar, no link navigation):
clet md --no-browse ./README.md
```
