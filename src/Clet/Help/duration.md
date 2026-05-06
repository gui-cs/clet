## Examples

```sh
# Duration picker:
clet duration

# Pre-filled (1 hour 30 minutes):
clet duration --initial "PT1H30M"

# With a title:
clet duration --title "Timeout duration"

# JSON output (ISO-8601 duration):
clet duration --json
# → {"schemaVersion":1,"status":"ok","value":"PT1H30M"}
```
