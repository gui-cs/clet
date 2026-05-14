## Examples

```sh
# Basic text prompt:
clet text

# With a title:
clet text --title "Enter your name"

# Pre-filled initial value:
clet text --initial "John Doe"

# Multi-line text editor:
clet text --rows 10

# Using the multiline-text alias:
clet multiline-text --title "Enter a commit message"

# JSON output:
clet text --json
# → {"schemaVersion":1,"status":"ok","value":"Hello world"}
```
