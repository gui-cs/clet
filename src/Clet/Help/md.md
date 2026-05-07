## Browser mode

By default, `md` runs in browser mode with a top navigation bar. Click local `.md`
links to navigate to them. Use **Ctrl+Left** / **Ctrl+Right** to go back and forward.
Fragment anchors (`file.md#heading`) scroll to the matching heading.

Use `--no-browse` to disable the top bar and local link navigation.

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

# View a file outside the working directory:
clet md --allow-file ../other-repo ../other-repo/README.md

# Disable browser mode (no top bar, no link navigation):
clet md --no-browse ./README.md
```
