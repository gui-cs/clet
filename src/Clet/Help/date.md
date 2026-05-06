## Examples

```sh
# Date picker:
clet date

# Pre-filled to today:
clet date --initial "2026-05-06"

# With a title:
clet date --title "Select deadline"

# JSON output (ISO-8601):
clet date --json
# → {"schemaVersion":1,"status":"ok","value":"2026-05-06"}
```
