## Examples

```sh
# Checkbox list (positional arguments):
clet multi-select "Read" "Write" "Execute"

# Comma-separated --options:
clet multi-select --options "Linux,macOS,Windows"

# Pre-selected items:
clet multi-select --initial "Read,Execute" "Read" "Write" "Execute"

# JSON output (array of selected texts):
clet multi-select --json "Red" "Green" "Blue"
# → {"schemaVersion":1,"status":"ok","value":["Red","Blue"]}
```
