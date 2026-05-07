#!/usr/bin/env pwsh
# scripts/doctor.ps1 — verify local prerequisites for clet development on Windows.
#
# PowerShell mirror of scripts/doctor.sh. Run from any shell:
#   pwsh -File scripts/doctor.ps1
#   .\scripts\doctor.ps1
#
# Checks the .NET SDK and the MSVC toolchain that AOT publishing
# (`dotnet publish ... -p:PublishAot=true`) shells out to. Surfaces missing
# pieces with a remediation pointer instead of letting MSB3073 surprise you.
#
# Exit codes: 0 = all checks passed, 1 = at least one prerequisite missing.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$script:Pass  = 0
$script:Fail  = 0
$script:Notes = New-Object System.Collections.Generic.List[string]

function Write-Ok   ([string]$msg) { Write-Host ("  ok    " + $msg) -ForegroundColor Green; $script:Pass++ }
function Write-Miss ([string]$msg, [string]$note) {
    Write-Host ("  miss  " + $msg) -ForegroundColor Red
    $script:Fail++
    [void]$script:Notes.Add($note)
}
function Write-Info ([string]$msg) { Write-Host ("  info  " + $msg) -ForegroundColor Yellow }

Write-Host "clet doctor — checking development prerequisites"
Write-Host ""

Write-Host ".NET SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $ver = (& dotnet --version 2>$null)
    Write-Ok "dotnet $ver"
    if ($ver -notlike "10.*") {
        Write-Info "clet targets net10.0 preview; non-10.x SDK may not restore."
    }
} else {
    Write-Miss "dotnet not found on PATH" `
              "Install .NET 10 SDK (preview): https://dotnet.microsoft.com/download/dotnet/10.0"
}
Write-Host ""

Write-Host "Native toolchain (required by 'make publish' / AOT)"

$vswhereDefault = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$vswhere = $null
if (Get-Command vswhere.exe -ErrorAction SilentlyContinue) {
    Write-Ok "vswhere.exe on PATH"
    $vswhere = "vswhere.exe"
} elseif (Test-Path $vswhereDefault) {
    Write-Ok "vswhere.exe at $vswhereDefault"
    $vswhere = $vswhereDefault
} else {
    Write-Miss "vswhere.exe not found" `
               "Install Visual Studio Build Tools 2022 with the 'Desktop development with C++' workload: https://aka.ms/vs/17/release/vs_BuildTools.exe"
}

# What AOT actually needs is the MSVC linker (link.exe). Check for it directly.
# This is more robust than querying a workload component ID, which can drift
# between VS editions (Build Tools, Community, Enterprise, Insiders/preview, etc.).
$linkOnPath = Get-Command link.exe -ErrorAction SilentlyContinue
$linkViaVswhere = $null
if ($vswhere) {
    # vswhere -find is what the dotnet AOT MSBuild target ultimately uses.
    # -prerelease is required to discover VS Insiders / preview installs.
    $linkViaVswhere = & $vswhere -latest -prerelease -products '*' `
        -find 'VC\Tools\MSVC\**\Hostx64\x64\link.exe' 2>$null | Select-Object -First 1
}

if ($linkOnPath) {
    Write-Ok ("link.exe on PATH: " + $linkOnPath.Source + " (Developer Command Prompt)")
} elseif ($linkViaVswhere) {
    Write-Ok ("MSVC linker discoverable via vswhere: " + $linkViaVswhere)
} else {
    Write-Miss "MSVC linker (link.exe) not found via PATH or vswhere" `
               "Install VS Build Tools 2022 with 'Desktop development with C++', or run from a Developer Command Prompt where link.exe is on PATH."
}

Write-Info "If AOT still fails with 'vswhere.exe is not recognized', that's a PATH issue: launch the build from a Developer Command Prompt or PowerShell where vswhere.exe (and link.exe) are reachable."
Write-Host ""

Write-Host ("Summary: {0} ok, {1} missing" -f $script:Pass, $script:Fail)
if ($script:Fail -gt 0) {
    Write-Host ""
    Write-Host "Remediation:"
    foreach ($n in $script:Notes) { Write-Host ("  - " + $n) }
    Write-Host ""
    Write-Host "See CONTRIBUTING.md -> Prerequisites for the full per-platform list."
    exit 1
}
exit 0
