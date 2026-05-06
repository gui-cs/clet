#!/usr/bin/env bash
# Demo: pick a markdown file with clet, then view it with clet md.
#
# Usage:
#   ./demos/file-then-md.sh [<root>]
#
# Runs from the local build (dotnet run), no global tool install needed.
# Requires: jq

set -euo pipefail

root="${1:-.}"
script_dir="$(cd "$(dirname "$0")" && pwd)"
project="$script_dir/../src/Clet"
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

# Step 1 — pick a markdown file
# --output writes JSON to a file so stdout stays free for the TUI
dotnet run --project "$project" -- file --json --filter "*.md" --root "$root" --output "$tmp"

status=$(jq -r '.status' "$tmp")
if [[ "$status" != "ok" ]]; then
  echo "No file selected (status: $status)" >&2
  exit 130
fi

file=$(jq -r '.value' "$tmp")
echo "Selected: $file" >&2

# Step 2 — view it
dotnet run --project "$project" -- md "$file"
