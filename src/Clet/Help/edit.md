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
```

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
