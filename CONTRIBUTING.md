# Contributing

Hell yes. We want your help.

## Prerequisites

clet builds and tests with just the .NET SDK. **AOT publishing** (`make publish`) additionally needs a platform-native linker — that's where local setups go wrong.

Run this first; it will tell you what (if anything) is missing:

```sh
make doctor
```

If you don't have `make`, run `bash scripts/doctor.sh` directly.

| Platform | Required for build/test | Required for `make publish` (AOT) |
|----------|-------------------------|------------------------------------|
| **macOS** | .NET 10 SDK (preview) | Xcode Command Line Tools (`xcode-select --install`) |
| **Linux** (Debian/Ubuntu) | .NET 10 SDK (preview) | `sudo apt install -y clang zlib1g-dev build-essential` |
| **Linux** (Fedora/RHEL) | .NET 10 SDK (preview) | `sudo dnf install -y clang zlib-devel` |
| **Windows** | .NET 10 SDK (preview) | Visual Studio Build Tools 2022 with the **"Desktop development with C++"** workload (provides the MSVC linker, the Windows SDK, and `vswhere.exe`). Either VS 2022 with the C++ workload or the standalone Build Tools installer works. |

.NET 10 SDK (preview): <https://dotnet.microsoft.com/download/dotnet/10.0>.
Windows Build Tools: <https://aka.ms/vs/17/release/vs_BuildTools.exe>.

If `make publish` fails on Windows with a `vswhere.exe is not recognized` or `MSB3073` error, the C++ workload is the missing piece — `make doctor` will say so.

## Quick start

```sh
make doctor      # one-time: verify your toolchain (or run scripts/doctor.sh)
make build
make test        # unit + integration + smoke
```

Or, without `make`:

```sh
dotnet restore
dotnet build
dotnet run --project tests/Clet.UnitTests
dotnet run --project tests/Clet.IntegrationTests
dotnet run --project tests/Clet.SmokeTests
```

All green? Ship it.

## How to contribute

1. **File an issue first** if the change is non-trivial. We'll align on the approach before you write code.
2. **Branch from `develop`**, not `main`. PRs target `develop`. Merging to `main` is a release.
3. **Keep PRs small.** One thing per PR. If your PR touches the spec, the decisions log, *and* the code — good, that's the doc-update gate doing its job.
4. **Tests are not optional.** New clet? Unit + integration tests. New CLI flag? CommandLineRoot tests. Bug fix? Regression test.
5. **Read `CLAUDE.md`** before your first PR. It has the build commands, the doc-update gate checklist, and pointers to the spec and decisions log.

## What we're looking for

- Bug reports with `clet --version` output and reproduction steps
- New clet ideas (file an issue first)
- Test coverage improvements
- Terminal compatibility fixes (especially Windows Terminal, iTerm2, GNOME Terminal)
- Documentation fixes

## Decisions log

Non-obvious choices go in `specs/decisions.md` (append at the bottom, never edit old entries). If your PR makes a choice a future reader might want to "fix," write it down.

## Code of conduct

Be kind. Be constructive. Ship code.
