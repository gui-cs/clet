#!/usr/bin/env bash
# scripts/doctor.sh — verify local prerequisites for clet development.
#
# Checks the .NET SDK and the platform-native toolchain that AOT publishing
# (`make publish`) shells out to. AOT failures from a missing linker show up
# as cryptic MSB3073 errors deep in MSBuild output; this script surfaces them
# up front with a remediation pointer.
#
# Exit codes: 0 = all checks passed, 1 = at least one prerequisite missing.

set -uo pipefail

PASS=0
FAIL=0
NOTES=()

ok()    { printf "  \033[32mok\033[0m    %s\n" "$1"; PASS=$((PASS+1)); }
miss()  { printf "  \033[31mmiss\033[0m  %s\n" "$1"; FAIL=$((FAIL+1)); NOTES+=("$2"); }
info()  { printf "  \033[33minfo\033[0m  %s\n" "$1"; }

uname_s="$(uname -s 2>/dev/null || echo Unknown)"

echo "clet doctor — checking development prerequisites"
echo

echo ".NET SDK"
if command -v dotnet >/dev/null 2>&1; then
  ver="$(dotnet --version 2>/dev/null || echo unknown)"
  ok "dotnet $ver"
  case "$ver" in
    10.*) : ;;
    *)    info "clet targets net10.0 preview; non-10.x SDK may not restore." ;;
  esac
else
  miss "dotnet not found on PATH" \
       "Install .NET 10 SDK (preview): https://dotnet.microsoft.com/download/dotnet/10.0"
fi
echo

echo "Native toolchain (required by 'make publish' / AOT)"
case "$uname_s" in
  Darwin)
    if xcode-select -p >/dev/null 2>&1; then
      ok "Xcode Command Line Tools at $(xcode-select -p)"
    else
      miss "Xcode Command Line Tools not installed" \
           "Run: xcode-select --install"
    fi
    if command -v clang >/dev/null 2>&1; then ok "clang $(clang --version | head -1)"; \
    else miss "clang not on PATH" "Install Xcode Command Line Tools: xcode-select --install"; fi
    ;;

  Linux)
    if   command -v clang >/dev/null 2>&1; then ok "clang $(clang --version | head -1)"
    elif command -v gcc   >/dev/null 2>&1; then ok "gcc   $(gcc --version | head -1)"
    else
      miss "no C compiler (clang or gcc) on PATH" \
           "Debian/Ubuntu: sudo apt install -y clang zlib1g-dev build-essential"
    fi
    # zlib is a runtime dep of the AOT'd binary on Linux.
    if [ -e /usr/include/zlib.h ] || ldconfig -p 2>/dev/null | grep -q libz.so; then
      ok "zlib development headers"
    else
      miss "zlib development headers not found" \
           "Debian/Ubuntu: sudo apt install -y zlib1g-dev   |   Fedora: sudo dnf install -y zlib-devel"
    fi
    ;;

  MINGW*|MSYS*|CYGWIN*|Windows_NT)
    # The dotnet AOT MSBuild target invokes vswhere.exe to locate the MSVC
    # linker. If vswhere isn't reachable, AOT fails with MSB3073 (exit 123).
    vswhere_default="/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe"
    if command -v vswhere.exe >/dev/null 2>&1; then
      ok "vswhere.exe on PATH"
      VSWHERE="vswhere.exe"
    elif [ -x "$vswhere_default" ]; then
      ok "vswhere.exe at default install location"
      VSWHERE="$vswhere_default"
    else
      VSWHERE=""
      miss "vswhere.exe not found" \
           "Install Visual Studio Build Tools 2022 with the 'Desktop development with C++' workload: https://aka.ms/vs/17/release/vs_BuildTools.exe"
    fi

    # What AOT actually needs is the MSVC linker (link.exe). Check for it directly
    # rather than querying a workload component ID — that drifts between VS
    # editions (Build Tools, Community, Enterprise, Insiders/preview).
    link_via_vswhere=""
    if [ -n "$VSWHERE" ]; then
      # -prerelease is required to discover VS Insiders / preview installs.
      link_via_vswhere="$("$VSWHERE" -latest -prerelease -products '*' \
        -find 'VC\Tools\MSVC\**\Hostx64\x64\link.exe' 2>/dev/null | tr -d '\r' | head -1)"
    fi

    if command -v link.exe >/dev/null 2>&1; then
      ok "link.exe on PATH (Developer Command Prompt)"
    elif [ -n "$link_via_vswhere" ]; then
      ok "MSVC linker discoverable via vswhere: $link_via_vswhere"
    else
      miss "MSVC linker (link.exe) not found via PATH or vswhere" \
           "Install VS Build Tools 2022 with 'Desktop development with C++', or run from a Developer Command Prompt where link.exe is on PATH."
    fi

    info "If AOT still fails with 'vswhere.exe is not recognized', that's a PATH issue: launch the build from a Developer Command Prompt or PowerShell where vswhere.exe (and link.exe) are reachable."
    ;;

  *)
    info "Unknown OS '$uname_s'; AOT prereqs not auto-checked. See CONTRIBUTING.md."
    ;;
esac
echo

echo "Summary: $PASS ok, $FAIL missing"
if [ "$FAIL" -gt 0 ]; then
  echo
  echo "Remediation:"
  for n in "${NOTES[@]}"; do printf "  - %s\n" "$n"; done
  echo
  echo "See CONTRIBUTING.md → Prerequisites for the full per-platform list."
  exit 1
fi
exit 0
