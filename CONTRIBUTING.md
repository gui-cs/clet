# Contributing

Hell yes. We want your help.

## Quick start

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
