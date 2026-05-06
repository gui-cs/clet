## Examples

```sh
# Pick text attributes (foreground, background, style):
clet attribute-picker

# Short alias:
clet attribute

# With a title:
clet attribute --title "Choose cell style"

# JSON output:
clet attribute --json
# → {"schemaVersion":1,"status":"ok","value":{"fg":"#ffffff","bg":"#000000","style":"bold"}}
```
