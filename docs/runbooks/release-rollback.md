# Release Rollback Runbook

> **Status:** DRAFT — exercised once before v0.9 RC per `specs/clet-spec.md` §7. Until that exercise has happened, treat every step here as "best guess; verify before executing."

> **Audience:** the maintainer paged at 3am for a `clet` release that escaped the §5.3 smoke gate. Assume you did not ship the bad release. Assume you have repo-admin on `gui-cs/clet`, push access to `gui-cs/homebrew-tap`, the WinGet PR-author cred, and the NuGet API key in a known location.

`clet` auto-publishes to channels on **two triggers** (D-020):

- **TG release tag** (stable channel): NuGet `Terminal.Gui.clet` (latest), Homebrew (gui-cs tap), WinGet (`microsoft/winget-pkgs`).
- **TG develop NuGet publish** (develop channel): NuGet `Terminal.Gui.clet` prerelease only (off `latest`; opt-in via `--prerelease`).

When the §5.3 smoke gate fails, the workflow halts and nothing reaches users — that case is an *aborted* release, not a *bad* release, and is out of scope for this runbook. This runbook covers the case where the gate let something through (a regression it didn't cover, a manifest bug, a signing failure mid-publish) and one or more channels carry a broken `clet`. **§2.4 covers the develop channel, which has different blast radius and SLAs.**

## 1. Triage (≤5 minutes)

Answer in order:

1. **What broke?** Failure mode in one sentence. Examples: "`clet --version` crashes on macOS arm64." "`clet pick-file --json` emits invalid JSON." "Binary won't start; missing libicu."
2. **What version?** From `clet --version` (or the channel's published version if `clet --version` is what's broken). Note the underlying TG version (they're 1:1).
3. **Which channels published?** Check in this order — Homebrew, WinGet, NuGet — and record `published / not-yet-published / failed-mid-publish` for each.
4. **Decision: rollback or forward-fix?**
   - **Forward-fix** (preferred) when the bug is small, the fix is < 1 hour, and channels are slow-update (most users haven't pulled yet). Tag a patch release; let the normal pipeline ship it.
   - **Rollback** when the bug is severe (data loss, hang, crash on first run), the fix is non-trivial, or a meaningful population has already pulled the bad version. Withdraw published artifacts per §2 below, then forward-fix on a slower clock.

If you are not sure, rollback. Re-publishing later is cheap; pulling back a bad binary that's been on a thousand laptops for six hours is not.

## 2. Per-channel withdrawal procedures

> Run withdrawals **in parallel** if you have help. Each channel is independent; nothing here serializes.

### 2.1 Homebrew tap (`gui-cs/homebrew-tap`)

**What happens to users:** Already-installed bad version stays on the user's machine until they `brew upgrade`. After withdrawal, `brew upgrade clet` resolves to the prior-known-good version.

**Steps:**

1. `git clone https://github.com/gui-cs/homebrew-tap`
2. Identify the commit that bumped `clet.rb` to the bad version.
3. **`git revert <sha>`** — never `--force` push, never `reset --hard`. The revert preserves the audit trail and re-pins to the prior version's bottle URLs and SHA256s.
4. `git push origin main` (or whatever the tap's default branch is).
5. Verify: on a fresh runner, `brew update && brew info gui-cs/tap/clet` should report the prior version.
6. Optional: post a one-liner to the tap repo's Discussions explaining the revert.

**Caveat:** Homebrew bottles are immutable on GitHub Releases by URL. The bad bottle URL still resolves; it's just no longer pointed to. If you need the bad bottle file *gone* (license/security reason), delete the asset from the GitHub Release manually — but `brew install` will then 404 for anyone who has the old formula in cache.

### 2.2 WinGet (`microsoft/winget-pkgs`)

**What happens to users:** WinGet caches manifests; users running `winget upgrade clet` after the manifest is removed will not see the bad version offered. Already-installed users keep the bad binary until the next `upgrade`.

**Steps:**

1. Fork `microsoft/winget-pkgs` if you don't already have a fork.
2. Delete the directory `manifests/g/gui-cs/clet/<bad-version>/` from the fork.
3. Open a PR titled `Remove gui-cs.clet <bad-version> (broken release)` with a one-paragraph explanation.
4. Microsoft's bot validates and merges if the manifest removal is clean. **Typical SLA: 2–24 hours.** There is no faster path; WinGet does not have an emergency-yank API.
5. Verify post-merge: `winget search clet` should no longer list the bad version.

**While the PR is pending,** consider posting a GitHub Release note on `gui-cs/clet` warning Windows users not to upgrade.

### 2.3 NuGet (`Clet.Tool`)

**What happens to users:** Existing installs continue to work. New installs of the unlisted version still resolve **if the user pins the version explicitly** — unlist hides from search/default-resolve, it does not hard-delete. New `dotnet tool install -g clet` (no version pin) resolves to the next-newest *listed* version.

**Steps:**

1. Sign in to `https://www.nuget.org` with the gui-cs account.
2. Navigate to `Manage Packages → Clet.Tool → <bad-version>`.
3. Click **Unlist** and confirm. (`dotnet nuget delete` is a list-only op, equivalent — use the web UI for the audit trail.)
4. Verify: `dotnet tool search clet` should not surface the bad version.
5. **Do not request a hard-delete from NuGet support** unless there's a security/IP reason. Hard-delete breaks `dotnet restore` for anyone who pinned, and removes the audit trail.

### 2.4 Develop channel (NuGet prerelease)

**What's different:** Develop builds publish to NuGet only, as prerelease versions (`X.Y.Z-develop.NN`). They do **not** appear as `latest` to default `dotnet tool install` consumers — only `--prerelease` users see them. Blast radius is therefore much smaller than the release channel.

**Steps when a develop build is bad:**

1. **Default action: do nothing.** TG develop publishes ~5–15× per week. The next develop publish supersedes within hours, and `--prerelease` users self-select for accepting some breakage. Filing an `incident:develop` issue (the workflow already does this on failure) is enough.
2. **Unlist only if** the bad build is causing active harm to a known user (data loss, hang, security issue) and the next develop publish is more than ~24 hours away. Procedure is identical to §2.3 — unlist via the NuGet web UI; do not hard-delete.
3. **Do not roll back the TG develop publish itself.** TG's develop branch is upstream of clet's; clet should not push back into TG's release cadence. If the bad clet develop is caused by a bad TG develop, file the issue against TG.

**What `--prerelease` consumers should expect:** the same risk profile as TG develop. We don't add a separate stability gate; we mirror what TG ships.

## 3. Cut a fast-follow patch

Once channels are withdrawn (or while the WinGet PR is pending):

1. Branch from the commit before the bad release: `git checkout -b fix/<TG_VERSION>-rollback`.
2. Apply the actual fix. Land it via normal PR review (do not `--no-verify`, do not skip CI).
3. Tag the patch. **Open question (resolve before v0.5):** what's the version scheme? Options under consideration: `<TG_VERSION>+rollback.1` (semver build metadata, ignored by most resolvers), `<TG_VERSION>-rollback.1` (pre-release, may sort wrong), or just `<TG_VERSION>.1` (cleanest, but breaks the strict 1:1-with-TG promise in §5.6 of the spec). Until decided, default to `<TG_VERSION>.1` and document the deviation in release notes.
4. Push the tag. The normal `release-on-tg-release.yml` workflow does **not** trigger (it listens for `repository_dispatch` from TG, not for tags here). Manually trigger the workflow with the new version, or — better — add a manual-dispatch (`workflow_dispatch`) entry point to the workflow that takes a version input. (This is itself a follow-up improvement.)
5. The §5.3 smoke gate gates the fix the same way it gates a normal release. If the gate fails, **do not bypass it.** Fix the smoke test or fix the binary, then re-run.

## 4. Post-incident

Within 48 hours of stabilization:

1. File an issue in `gui-cs/clet` titled `Incident: <TG_VERSION> rollback`. Tag `incident`. Include: timeline (UTC), trigger, blast radius (channels affected, estimated user count if available), root cause, fix.
2. Add the failure mode as a new case to `tests/Clet.SmokeTests` (so a future regression of the same shape is caught) and to the §6.8 release-pipeline dry-run cases (so a future pipeline regression of the same shape is caught).
3. If a runbook step here was wrong or missing, **edit this file** in the same PR. The runbook must end the incident better than it started it.

## 5. What we will not do

- **No `git push --force` to the tap repo.** Audit trail is the priority; reverts are forward-only.
- **No NuGet hard-delete.** Unlist is sufficient and reversible. Hard-delete is permanent and breaks pinned restores.
- **No auto-revert.** The `release-on-tg-release.yml` workflow does not auto-roll-back on smoke failure; it halts and pages. Humans decide.
- **No bypassing the smoke gate** during a rollback re-publish, however urgent. The gate is the only thing that kept the bad release from being a worse release.
- **No skipping signing/notarization** to ship a fast fix. An unsigned macOS binary is a *different* kind of broken release.

## Open questions (resolve before v0.9)

- **Tag scheme for rollback patches** (see §3 step 3 above).
- **On-call rotation.** Who carries the pager during release weeks? `gui-cs` does not currently have a rotation; until it does, "the person who tagged the TG release" is the de facto owner.
- **Paging channel.** GitHub release-issue auto-comment is not a pager. Matrix? Discord? Email-to-SMS? Decide and document here.
- **WinGet emergency contact.** Is there a faster path for Microsoft-bot-merged manifest removal in a security incident? Investigate before v0.9.
- **Asciinema artifact retention.** TUIcast captures a `.cast` per smoke run (§5.3). Retention policy? Indefinitely is cheap; document explicitly so post-incident replays are guaranteed available.
