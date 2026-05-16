## Examples

```sh
# Edit an existing file:
clet edit foo.cs

# Edit a new file (created on first save):
clet edit newfile.txt

# Open with no file (use File > Open or Save As):
clet edit

# Open in read-only mode:
clet edit --readonly foo.cs

# With a custom title:
clet edit --title "Config Editor" settings.json

# Edit a file outside the working directory (ephemeral override):
clet edit --allow-file /path/to/dir /path/to/dir/file.cs
```

## File access

By default, `clet edit` is restricted to files in the current working directory.
When you open a file outside the allowed directories, a dialog appears offering:

- **Allow once** — permits the file for this session only.
- **Add to config** — permanently adds the directory to the allow list in
  `~/.tui/clet.config.json`.
- **Cancel** — aborts the edit.

You can also allow access up front:

```
clet edit --allow-file /path/to/dir /path/to/dir/file.cs
```

Or permanently via `~/.tui/clet.config.json`:

```jsonc
{
  "FileAccessSettings.AllowedPaths": [
    "/home/user/projects",
    "/home/user/docs"
  ]
}
```

Run `clet config` to open the config file.

## Default keyboard shortcuts

| Key | Action |
|-----|--------|
| Ctrl+N | New file |
| Ctrl+O | Open file |
| Ctrl+S | Save |
| Ctrl+Q | Quit |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+X | Cut |
| Ctrl+C | Copy |
| Ctrl+V | Paste |
| Ctrl+A | Select all |

All keyboard shortcuts are configurable via `~/.tui/clet.config.json`.
