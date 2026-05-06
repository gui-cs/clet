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
```
