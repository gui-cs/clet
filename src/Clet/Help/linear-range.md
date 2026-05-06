## Examples

### Single selection (default)

```sh
clet range "Free" "Pro" "Team" "Enterprise"
# or
clet range --options "Free,Pro,Team,Enterprise"
```

JSON output:
```json
{"schemaVersion":1,"status":"ok","value":{"mode":"single","value":"Pro","index":1}}
```

### Multi selection

```sh
clet range --mode multi --options "Read,Write,Execute"
```

JSON output:
```json
{"schemaVersion":1,"status":"ok","value":{"mode":"multi","values":["Read","Execute"],"indices":[0,2]}}
```

### Bounded range

```sh
clet range --mode range "S" "M" "L" "XL" "XXL"
# With initial selection:
clet range --mode range --initial "M..XL" "S" "M" "L" "XL" "XXL"
```

JSON output:
```json
{"schemaVersion":1,"status":"ok","value":{"mode":"range","kind":"closed","start":"M","end":"XL","startIndex":1,"endIndex":3}}
```

### Numeric range

Generate numeric options in the shell — labels are strings, so any
sequence works:

```sh
# Pick a value from 0.0 to 10.0 in 0.1 increments:
clet range $(seq 0 0.1 10.0)

# Select a sub-range:
clet range --mode range $(seq 0 0.1 10.0)
```

```powershell
# PowerShell equivalent:
clet range (0..100 | ForEach-Object { $_ / 10 })
```

### Range kinds

```sh
# Left-bounded (everything up to an endpoint):
clet range --mode range --range-kind left "Jan" "Feb" "Mar" "Apr"

# Right-bounded (everything from a starting point):
clet range --mode range --range-kind right "Jan" "Feb" "Mar" "Apr"
```

### Layout and display

```sh
# Vertical orientation:
clet range --orientation vertical "Low" "Medium" "High"

# Hide option legends:
clet range --hide-legends true "A" "B" "C" "D"

# Allow empty selection:
clet range --allow-empty true "Red" "Green" "Blue"
```
