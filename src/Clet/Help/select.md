## Examples

```sh
# Positional arguments:
clet select "prod" "staging" "dev"

# Comma-separated --options:
clet select --options "prod,staging,dev"

# With an initial selection:
clet select --initial "staging" "prod" "staging" "dev"

# JSON output for scripting:
clet select --json "prod" "staging" "dev"
# → {"schemaVersion":1,"status":"ok","value":"staging"}
```
