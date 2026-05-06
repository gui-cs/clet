# CI/CD Workflows

## Branching model

| Branch | Purpose | Default? |
|--------|---------|----------|
| `develop` | Day-to-day work. PRs target here. | Yes |
| `main` | Releases only. Merge `develop` → `main` to ship. | No |

## Versioning (D-023)

Version is controlled by `<Version>` in `src/Clet/Clet.csproj`. The release workflow auto-increments a build number on each run.

| Phase | csproj `<Version>` | main produces | develop produces |
|-------|--------------------|---------------|------------------|
| Alpha | `1.0.0-alpha` | `v1.0.0-alpha.1`, `.2`, `.3` ... | `v1.0.0-develop.1`, `.2`, `.3` ... |
| Beta | `1.0.0-beta` | `v1.0.0-beta.1`, `.2`, ... | `v1.0.0-develop.N` ... |
| Stable | `1.0.0` | `v1.0.0`, `v1.0.1`, `v1.0.2` ... | `v1.0.1-develop.1`, ... |

To move between phases, change `<Version>` in the csproj and merge to main.

Build numbers auto-increment by finding the latest matching git tag (`v1.0.0-alpha.*`, `v1.0.0-develop.*`, etc.).

## Workflows

### `ci.yml` — Continuous Integration

Runs on every push and every PR targeting `develop` or `main`.

- Restore, build, unit tests, integration tests, smoke tests
- No publishing, no tagging

### `release.yml` — Build, Test, Tag, Publish

**Triggers:**

| Trigger | When | Channel |
|---------|------|---------|
| Push to `main` (changes in `src/` or `tests/`) | Merge develop → main | main |
| Push to `develop` (changes in `src/` or `tests/`) | PR merge to develop | develop |
| `repository_dispatch` from Terminal.Gui | TG release or develop publish | develop |
| `workflow_dispatch` (manual) | Rollback patches, dry-runs | depends on branch |

**Pipeline:**

```
resolve-version → build (3 RIDs) → tag → publish-nuget
                                       → publish-homebrew (stable only)
                                       → publish-winget (stable only)
                                       → notify-failure (on error)
```

**Build matrix:** `osx-arm64`, `linux-x64`, `win-x64`. Each RID builds AOT, runs unit + integration + smoke tests, uploads artifacts.

**Tagging:** Every successful build is tagged (`v1.0.0-alpha.3`, `v1.0.0-develop.5`, etc.) so future runs can find the latest build number.

**NuGet:** Both channels publish to package id `clet` (see [D-024](../../specs/decisions.md)). Prerelease versions (`-alpha`, `-develop`) are hidden from default `dotnet tool install -g clet`; consumers opt in with `--prerelease`.

**Homebrew / WinGet:** Only on stable main releases (version has no `-` suffix). Both are placeholders until `gui-cs/homebrew-tap` exists and WinGet tooling is wired (D-012).

## Terminal.Gui version

The TG dependency version is set in the csproj as `<TerminalGuiVersion>`. The release workflow can override it via:

- `repository_dispatch` payload: `client_payload.tg_version`
- `workflow_dispatch` input: `tg_version`
- MSBuild property: `-p:TerminalGuiVersion=2.0.3`

See `specs/decisions.md` D-020 and D-023.

## Secrets and variables

| Name | Type | Used by | Purpose |
|------|------|---------|---------|
| `NUGET_API_KEY` | Secret | `publish-nuget` | Push packages to nuget.org |
| `HOMEBREW_TAP_TOKEN` | Secret | `publish-homebrew` | Push to `gui-cs/homebrew-tap` |
| `HOMEBREW_TAP_ENABLED` | Variable | `publish-homebrew` | Set to `true` to enable |
| `WINGET_ENABLED` | Variable | `publish-winget` | Set to `true` to enable |

## Rollback

See `docs/runbooks/release-rollback.md`.

## Related decisions

- **D-023** — Two-branch versioning (main + develop)
- **D-022** — Independent versioning from TG (superseded by D-023, principle retained)
- **D-020** — TG dispatch types and MSBuild version variable
- **D-012** — Code signing deferred post-1.0
