#!/usr/bin/env pwsh
# Demo: pick a markdown file with clet, then view it with clet md.
#
# Usage:
#   ./demos/file-then-md.ps1 [<root>]
#
# Runs from the local build (dotnet run), no global tool install needed.

param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot ".." "src" "Clet"
$tmp = [System.IO.Path]::GetTempFileName()

try {
    # Step 1 — pick a markdown file
    # --output writes JSON to a file so stdout stays free for the TUI
    dotnet run --project $project -- file --json --filter "*.md" --root $Root --output $tmp

    $result = Get-Content $tmp -Raw | ConvertFrom-Json

    if ($result.status -ne "ok") {
        Write-Error "No file selected (status: $($result.status))"
        exit 130
    }

    $file = $result.value
    Write-Host "Selected: $file" -ForegroundColor Cyan

    # Step 2 — view it
    dotnet run --project $project -- md $file
}
finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
