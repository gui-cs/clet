## Examples

```sh
# Show the main help overview (TUI viewer):
clet help

# Render to stdout instead of the TUI:
clet help --cat

# Show help for a specific clet:
clet help select
clet help range
clet help md

# Equivalent forms — all work:
clet select help
clet select --help
clet select -h

# Render a clet's help to stdout:
clet help select --cat
clet select help --cat
```

## Reporting Problems

Found a bug or have a suggestion? [File an issue on GitHub](https://github.com/gui-cs/clet/issues/new).

Include `clet --version` output, your terminal and OS, and what you ran vs. what happened.
