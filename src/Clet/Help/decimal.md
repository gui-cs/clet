## Examples

```sh
# Decimal spinner:
clet decimal

# With step size:
clet decimal --step 0.1

# Pre-filled value:
clet decimal --initial "3.14"

# With a title:
clet decimal --title "Enter price" --step 0.01 --initial "9.99"

# JSON output:
clet decimal --json
# → {"schemaVersion":1,"status":"ok","value":9.99}
```
