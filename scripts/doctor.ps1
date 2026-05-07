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

if ($vswhere) {
    # Look for an installation that has the MSVC linker — that's what AOT actually needs.
    $vsInstall = & $vswhere -latest -products '*' `
        -requires 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' `
        -property installationPath 2>$null
    if ($vsInstall) {
        Write-Ok "MSVC C++ toolchain at $vsInstall"
    } else {
        Write-Miss "Visual Studio C++ workload (VC.Tools.x86.x64) not installed" `
                   "In the VS Installer, modify your install and add 'Desktop development with C++'."
    }
}

Write-Info "If 'make publish' still fails after installing the C++ workload, also ensure a Windows 10/11 SDK is checked in the VS Installer."
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
