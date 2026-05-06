## Examples

```sh
# Integer spinner:
clet int

# With step size:
clet int --step 5

# Pre-filled value:
clet int --initial "42"

# With a title:
clet int --title "How many replicas?" --initial "3"

# JSON output:
clet int --json
# → {"schemaVersion":1,"status":"ok","value":42}
```
