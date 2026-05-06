## Examples

```sh
# Time picker:
clet time

# Pre-filled:
clet time --initial "14:30:00"

# With a title:
clet time --title "Meeting start time"

# JSON output (ISO-8601):
clet time --json
# → {"schemaVersion":1,"status":"ok","value":"14:30:00"}
```
