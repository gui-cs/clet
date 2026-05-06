## Examples

```sh
# File picker (tree dialog):
clet pick-file

# Short alias:
clet file

# Start in a specific directory:
clet file --root ./src

# Filter by extension:
clet file --filter "*.cs"

# Allow multiple selection:
clet file --multi true

# JSON output:
clet file --json --root ./src --filter "*.md"
# → {"schemaVersion":1,"status":"ok","value":"/path/to/README.md"}

# Multi-select JSON (array):
clet file --json --multi true
# → {"schemaVersion":1,"status":"ok","value":["/path/a.md","/path/b.md"]}
```
