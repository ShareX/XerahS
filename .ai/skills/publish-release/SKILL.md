---
name: publish-release
description: Publish a requested XerahS release or prepare a Windows portable package. Local packaging does not authorize publishing.
---

# XerahS Release Bump Tag

## Windows Portable Releases

Windows portable ZIPs are part of every normal tagged release alongside the EXE/MSI installers. For portable-only local packaging, artifact verification, and upgrade guidance, read [references/windows-portable.md](references/windows-portable.md). A request to build a portable ZIP locally does not require a version bump, tag, or GitHub release; use that reference instead of the publishing sequence below.

## Overview

Use this skill to run release steps in strict order. Agents: write the changelog section (Step 2) **before** running the primary command with `--assume-changelog-done`; the script refuses to bump/tag without it.

Steps:
- Step 1: Execute maintenance prep first (`git pull --recurse-submodules` and `git submodule update --init --recursive`), then reattach `ShareX.ImageEditor` to `develop` and fast-forward it from `origin/develop`. Uncommitted local changes in the working tree are auto-committed with message `[skill] Auto-commit uncommitted changes before release maintenance` before the pull. Submodule local changes still block the sequence and must be committed/stashed manually.
- Step 2: Run the `update-changelog` skill (`.ai/skills/update-changelog/SKILL.md`) for the **release version** (the post-bump version, not the current `Directory.Build.props` value). See "Step 2 performs" below. The sequence script blocks Step 3 until `docs/CHANGELOG.md` has that version's section.
- Step 3: Verify build, then execute bump/commit/push/tag automation
- Step 4: Monitor the tag-triggered release workflow every 2 minutes
- Step 5: If failure occurs, inspect logs, fix the root cause in code or workflow first (do not skip the fix and retry the same version), commit and push the fix, then retry with the next patch version. If the failure is in the `build-flatpak` job, verify all plugin DLLs are present in the staging directory; the workflow now includes a plugin re-validation step but a code fix in `package-linux.sh` may also be needed. Repeat until workflow succeeds.
- Step 6: Ensure standard release notes block is present on the GitHub release
- Step 7: Apply repo release-channel policy (see below)
- Optional Step 8: Generate a Flathub source-build manifest candidate from the successful release tag; do not open or automate a Flathub PR
- Optional Step 9: Stamp Launchpad PPA / Fedora COPR / openSUSE OBS candidates, or publish them with `--publish-distro-repos` (secrets-gated skip per backend; see `docs/linux/distro-repos.md`)
- GitHub Actions release upload steps must match the same repo policy (`prerelease` / `make_latest` by `github.repository`); do not rely only on the post-workflow `gh release edit` guard.

Repository target behavior (dual-repo):
- Supported release targets: `https://github.com/KovaForge/XerahS` and `https://github.com/ShareX/XerahS`.
- Git pushes use `--push-remote` (default: `origin`). For ShareX publishes from a KovaForge fork checkout, use `--push-remote upstream --repo ShareX/XerahS`.
- GitHub CLI operations (`gh run`, `gh release`) resolve from the `origin` remote URL by default.
- Origin may be a standard `github.com` URL or a KovaForge per-person SSH alias such as `git@github-vladislava:KovaForge/XerahS.git`.
- Do **not** rely on bare `gh repo view` for target inference on fork checkouts: it often resolves the upstream parent (`ShareX/XerahS`) instead of origin (`KovaForge/XerahS`).
- Use `--repo owner/name` to override the inferred target when needed.
- After a successful workflow, the skill verifies the required asset set on the chosen repo (Windows installers and portable ZIPs/macOS/Linux/Flatpak + Chocolatey nupkg).

Release channel policy (default):
- `ShareX/XerahS` -> always **Pre-release** (`prerelease: true`, not latest)
- `KovaForge/XerahS` -> **Release** / latest (`prerelease: false`, `make_latest: true`)
- Override with `--set-prerelease` or `--no-prerelease` when intentionally forcing the opposite channel.

Step 2 performs (update-changelog integration):
- Resolves the release version from `Directory.Build.props` plus `--bump` (prompts once for the bump type if neither `--bump` nor `--no-bump` was passed, then reuses it in Step 3).
- Without `--assume-changelog-done`: writes a draft with `.ai/skills/update-changelog/scripts/update-changelog.sh --version <release-version> --output-path dist/changelog/draft-v<release-version>.md` (needs `pwsh`; skipped with a message otherwise), then waits for `done`.
- The agent follows the update-changelog skill: lower bound is the latest tag **on the target repo** (pass `--from-tag`; local `git describe` can pick a stale tag on fork or shallow checkouts), rewrite the draft into release-note prose, add `## v<release-version>` under the preamble, fold any untagged intermediate sections into it or keep them if they shipped on the other repo.
- Always verifies `docs/CHANGELOG.md` contains `## v<release-version>` (or the linked form) and aborts otherwise. `--assume-changelog-done` only skips the draft and prompt; it does not skip this check. Use `--skip-changelog` for an explicit bypass, or when `docs/CHANGELOG.md` is absent.
- Do not commit the changelog separately: Step 3 stages all changes, so the changelog ships in the `[vX.Y.Z] [CI] Release vX.Y.Z` commit.

Step 3 performs:
- Pre-check: Run `dotnet build src/desktop/XerahS.sln`; do not proceed if build fails.
- Prompts for `x/y/z` bump type (major/minor/patch) unless specified.
- Updates every tracked `Directory.Build.props` file that defines `<Version>`.
- Syncs `build/windows/chocolatey/xerahs.nuspec` `<version>` with the release version.
- Stages all current repo changes.
- Commits with version-prefixed message.
- Pushes current branch and creates/pushes annotated tag `vX.Y.Z`.

Step 4-5 performs:
- Find tag run for `Release Build (All Platforms)`.
- Poll run status every 120 seconds until completion.
- On failure, inspect failing job logs and identify first blocking error.
- Fix root cause in code/workflow/scripts.
- Re-run local pre-check build.
- Retry release using next patch bump, then monitor again.
- Repeat until workflow succeeds.

Step 6 performs:
- Ensures release notes always include:
  - `Change log:`
  - `https://xerahs.com/changelog.html`
  - `### macOS Troubleshooting ("App is damaged")` section with Gatekeeper `xattr -cr` guidance.
- After the release is published, the tag workflow also builds, smoke-tests, and attaches `xerahs.X.Y.Z.nupkg` to the GitHub release.
- `build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version X.Y.Z` remains the manual recovery path for re-syncing checksums or repacking.
- Expected Windows release assets: six files across x64 and ARM64, with `XerahS-X.Y.Z-win-<arch>.exe`, `XerahS-X.Y.Z-win-<arch>.msi`, and `XerahS-X.Y.Z-win-<arch>-portable.zip` for each architecture. The ZIP must contain `portable.txt` next to `XerahS.exe`; see the portable reference for validation.
- Expected Linux AppImage assets: `XerahS-X.Y.Z-linux-x64.AppImage`, `XerahS-X.Y.Z-linux-arm64.AppImage` (in addition to tar.gz/deb/rpm and the existing Flatpak bundle).

Optional Step 8 performs:
- Runs `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh --tag vX.Y.Z --repo owner/name --lint`.
- Generates `dist/flathub/com.xerahs.XerahS.yml` from the GitHub release tag plus pinned `ShareX.ImageEditor` and `Omacut` submodule commits.
- Adds the Freedesktop SDK `dotnet10` and `node24` extensions needed to run the Linux publish script inside the Flatpak build sandbox.
- Verifies the generated manifest does not use local `dist/xerahs-flatpak-staging` sources.
- Flags missing offline NuGet/.NET dependency source artifacts. A release is not Flathub-ready until the generated dependency sources are present and a network-disabled Flatpak source build passes. The video editor is native .NET (Omacut), so there are no npm sources.
- Keeps this as a pre-release validation path. Do not mark a release stable for Flathub until the source-build manifest, dependency sources, manifest lint, repo lint, and manual smoke tests pass.

Optional Step 9 performs:
- `--prepare-distro-repo-source` runs `.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo owner/name` and stamps `dist/distro-repo/` from `build/linux/repo-staging/`.
- `--publish-distro-repos` runs `.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag vX.Y.Z --repo owner/name`: stamp, then `dput` / `copr-cli build` / `osc commit` when credentials exist.
- Missing Launchpad GPG, COPR config, or OBS login skips that backend and does not fail the release. A backend with credentials that fails the upload does fail.
- One-time project creation and secret names live in `docs/linux/distro-repos.md`.
- Does not invent a second `.deb` / `.rpm` format. GitHub release assets remain the one-off installers; these channels are the `apt` / `dnf` / `zypper` update path from ShareX/XerahS#253.

## Primary Command

From repository root:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh
```

Automated monitor with repo-default channel (recommended):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --bump z --yes
```

KovaForge fork example (publishes as full Release / latest):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --repo KovaForge/XerahS --git-wrapper git-vladislava --push-remote vladislava --assume-changelog-done --monitor --bump z --yes
```

ShareX upstream example from a KovaForge checkout (publishes as Pre-release):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --repo ShareX/XerahS --push-remote upstream --assume-changelog-done --monitor --bump z --yes
```

Force pre-release on KovaForge (override):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --repo KovaForge/XerahS --assume-changelog-done --monitor --set-prerelease --bump z --yes
```

Force stable/latest on ShareX (override; uncommon):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --repo ShareX/XerahS --push-remote upstream --assume-changelog-done --monitor --no-prerelease --bump z --yes
```

Manual monitor (fallback, PowerShell example):

```powershell
gh run list --limit 10 --json databaseId,workflowName,headBranch,status,conclusion,url
Start-Sleep -Seconds 120
gh run view <run-id> --json status,conclusion,jobs,url
```

## Non-Interactive Examples

Patch bump, no prompts:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump z --yes
```

Patch bump with built-in 2-minute monitoring:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --monitor-interval 120 --bump z --yes
```

Patch bump, pre-release forced, and generate Flathub source-build candidate:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --set-prerelease --prepare-flathub-source --bump z --yes
```

Patch bump and stamp PPA/COPR/OBS candidates (does not publish):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --prepare-distro-repo-source --bump z --yes
```

Patch bump and publish PPA/COPR/OBS (skips a backend without credentials):

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --monitor --publish-distro-repos --bump z --yes
```

Stamp or publish an existing tag without running the rest of the sequence:

```bash
./.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag vX.Y.Z --repo KovaForge/XerahS
./.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag vX.Y.Z --repo KovaForge/XerahS
```

Minor bump with custom commit token/summary:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump y --type CI --summary "Prepare release artifacts" --yes
```

Preview only:

```bash
./.ai/skills/publish-release/scripts/run-release-sequence.sh --assume-changelog-done --bump z --dry-run --yes
```

## When bash is unavailable (e.g. Windows PowerShell)

On environments where `bash` is not in PATH, execute the sequence manually:

1. Step 1 - Maintenance
   - `git pull --recurse-submodules`
   - `git submodule update --init --recursive`
   - Mandatory after submodule update: `git -C ShareX.ImageEditor fetch origin --prune`
   - Mandatory after submodule update: `git -C ShareX.ImageEditor checkout develop`
   - Mandatory after submodule update: `git -C ShareX.ImageEditor pull --ff-only origin develop`
   - Verify `git -C ShareX.ImageEditor status --short --branch` shows `develop...origin/develop`, not detached HEAD.
   - Abort if `ShareX.ImageEditor` has local changes, cannot fast-forward, or remains detached. If this updates the recorded submodule commit, commit and push the submodule before committing the parent XerahS gitlink.

2. Step 2 - Changelog
   - Compute the release version first (Step 3 rules below), then run `.ai/skills/update-changelog/SKILL.md` for that version with `-FromTag <latest tag on target repo>`.
   - Confirm `docs/CHANGELOG.md` has `## v<new-version>` before continuing.
   - Skip only if `docs/CHANGELOG.md` is intentionally absent or the user confirms skip.

3. Step 3 - Bump, commit, push, tag
   - Run `dotnet build src/desktop/XerahS.sln`; abort if it fails.
   - Read current version from root `Directory.Build.props`.
   - Compute next version: patch `Z+1`, minor `Y+1.0`, major `X+1.0.0`.
    - Ensure tag `v<new-version>` does not exist locally or on `origin`.
    - PowerShell-safe local check (avoid false positives from `if (git rev-parse <tag>)`):
       - `git show-ref --verify --quiet "refs/tags/v<new-version>"`
       - `if ($LASTEXITCODE -eq 0) { throw "Local tag exists" }`
    - PowerShell-safe remote check:
       - `git ls-remote --exit-code --tags origin "refs/tags/v<new-version>" *> $null`
       - `if ($LASTEXITCODE -eq 0) { throw "Remote tag exists" }`
    - For `--no-bump`: if `v<current-version>` already exists, do not try to recreate it. Use `--no-tag` for commit-only flow, or bump patch for a new tag.
   - Update all tracked `Directory.Build.props` files containing `<Version>`.
   - Update `build/windows/chocolatey/xerahs.nuspec` `<version>` to match.
   - `git add -A` -> `git commit -m "[v<new-version>] [CI] Release v<new-version>"` -> `git push origin <current-branch>` -> `git tag -a v<new-version> -m "v<new-version>"` -> `git push origin v<new-version>`.

4. Step 4 - Monitor every 2 minutes
   - Find run: `gh run list --limit 10 --json databaseId,workflowName,headBranch,status,conclusion,url`
   - Poll: `Start-Sleep -Seconds 120`; then `gh run view <run-id> --json status,conclusion,jobs,url`

5. Step 5 - On failure, fix and retry
   - Fetch failed job logs: `gh run view <run-id> --job <job-id> --log`
   - Fix root cause in repository.
   - Re-run `dotnet build src/desktop/XerahS.sln`.
   - Repeat Step 3 with next patch version.

6. Step 6 - Ensure standard release notes content
   - Read current body: `gh release view v<new-version> --json body`
   - Append the standard changelog + macOS troubleshooting block if missing.
   - Write body: `gh release edit v<new-version> --notes-file <file>`
   - Verify all **6 Windows assets** are attached (EXE, MSI, and `-portable.zip` for both `win-x64` and `win-arm64`) plus macOS and Linux assets. Validate portable ZIP contents as described in [references/windows-portable.md](references/windows-portable.md), not just filenames.

7. Step 7 - Apply release channel policy
   - `ShareX/XerahS`: `gh release edit v<new-version> --prerelease --latest=false`
   - `KovaForge/XerahS`: `gh release edit v<new-version> --prerelease=false --latest`
   - Verify: `gh release view v<new-version> --json isPrerelease,url,assets` and `gh api repos/<owner>/<repo>/releases/latest --jq .tag_name` (gh has no `isLatest` field)
   - Overrides: `--set-prerelease` / `--no-prerelease`
   - Workflow guard: `.github/workflows/release-build-all-platforms.yml` must create/upload with the same repo policy (`prerelease: github.repository != 'KovaForge/XerahS'`, `make_latest: github.repository == 'KovaForge/XerahS'`).

8. Optional Flathub source-build preparation for manual submission
   - Prefer keeping ShareX validation releases as pre-release while Flathub work is ongoing.
   - Run `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh --tag v<new-version> --repo owner/name --lint`.
   - Confirm the generated manifest uses `type: git` sources pinned by tag/commit for the main repository and submodules.
   - Generate and add offline dependency sources for NuGet/.NET packages before attempting a network-disabled Flathub build.
   - Build and lint the generated manifest locally before a human maintainer manually opens the Flathub PR.

9. Optional PPA / COPR / OBS
   - Stamp only: `.ai/skills/publish-release/scripts/prepare-distro-repo-assets.sh --tag v<new-version> --repo owner/name`.
   - Publish: `.ai/skills/publish-release/scripts/publish-distro-repos.sh --tag v<new-version> --repo owner/name` (or `--publish-distro-repos` on the sequence script).
   - Confirm Source0 / `_service` URLs point at `XerahS-<version>-linux-x64.tar.gz` (or arm64), not a doubled `linux-linux-` path.
   - Secret names and one-time Launchpad / COPR / OBS setup: `docs/linux/distro-repos.md`.

10. Optional post-release Chocolatey maintenance
   - The tag workflow should already have produced and smoke-tested `xerahs.<new-version>.nupkg`.
   - Manual repack/re-sync: `powershell -File build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version <new-version> -Pack`
   - Manual smoke test: `powershell -File build/windows/chocolatey/Test-ChocolateyPackage.ps1 -Version <new-version> -SourceDirectory dist\chocolatey`
   - Optionally push after review: `powershell -File build/windows/chocolatey/Sync-ChocolateyPackage.ps1 -Version <new-version> -Pack -Push -ApiKey <key>`

Default bump when unspecified: patch (`z`). Default commit type token: `CI`.

## Behavior

1. Require completion of `run-maintenance` first.
   - Script behavior: executes maintenance commands automatically unless explicitly bypassed with `--skip-maintenance` (or legacy alias `--assume-maintenance-done`).
   - Uncommitted local working-tree changes are auto-committed before the pull; submodule changes still block and require manual resolution.
2. Require completion of `update-changelog` second for the release version; the script verifies the `## v<release-version>` section exists (skip only with `--skip-changelog`, if `docs/CHANGELOG.md` is intentionally absent, or user confirms).
3. Before bump, run `dotnet build src/desktop/XerahS.sln`; abort on failure.
4. Run `scripts/bump-version-commit-tag.sh` (or PowerShell/manual equivalent when bash unavailable).
5. After tag push, monitor the release workflow every 120 seconds until complete.
6. If failed, inspect logs, fix root cause, and retry with next patch version.
7. Continue retry loop until release workflow is successful.
8. Ensure standard release notes content is present on the successful release.
9. Apply release-channel policy for the target repo: ShareX/XerahS = pre-release; KovaForge/XerahS = full latest release.
10. When preparing for Flathub, generate the source-build manifest candidate and treat missing offline dependency sources as release-blocking for Flathub submission.

## Guardrails

- Do not skip sequence unless user explicitly requests bypass.
- Do not skip maintenance unless user explicitly requests bypass (`--skip-maintenance`). Local working-tree changes are auto-committed before the pull rather than blocking.
- Do not commit/push during maintenance/changelog steps.
- After maintenance submodule update, always reattach `ShareX.ImageEditor` to `develop`, fast-forward it from `origin/develop`, and verify it is not detached before build, bump, tag, or release work continues.
- Always verify build before bump/tag.
- Always monitor workflow after tag push; do not stop at tag creation.
- Always inspect logs on failure and fix root cause before retry.
- Always ensure the standard release notes block exists on the successful release.
- Always keep Flathub validation on ShareX/XerahS as pre-release until source-build, dependency-source, lint, repo-lint, and smoke-test gates pass.
- Always keep the GitHub Actions release creation step aligned with repo channel policy: ShareX/XerahS uses `prerelease: true` / `make_latest: false`; KovaForge/XerahS uses `prerelease: false` / `make_latest: true`.
- Always use a new patch version for retries requiring new commits/tags.
- Abort on detached HEAD.
- Abort if version format is not `X.Y.Z`.
- Abort if matching tag already exists locally or remotely.
- In PowerShell manual flow, use `git show-ref --verify --quiet "refs/tags/<tag>"` and `git ls-remote --exit-code --tags` with `$LASTEXITCODE` checks for tag existence.
- Support `--no-push` and `--no-tag` when partial flow is needed.

## Agent usage (Cursor / Codex)

When executing this skill:
1. Run sequence: maintenance -> changelog (update-changelog skill for the release version, then `--assume-changelog-done`) -> build verify -> bump/commit/push/tag.
2. Use bash scripts if bash exists; otherwise use PowerShell/manual flow.
3. Default bump is patch (`z`) when unspecified.
4. Monitor tag workflow every 120 seconds until completion.
5. On failure, inspect logs, fix issue, and retry with next patch version.
6. Ensure release notes include changelog link + macOS troubleshooting block.
8. If requested explicitly, override channel with `--set-prerelease` or `--no-prerelease`; otherwise apply repo defaults.
9. If preparing for Flathub, run the source-build helper and report which of the source/dependency gates passed or failed.
10. If preparing first-party Linux repos, run the distro-repo stamp helper. If publishing, run `publish-distro-repos.sh` and report which backends uploaded vs skipped.
11. Report final version, commit hash, branch push status, tag push status, run URL, repo target, and release channel (pre-release vs latest).

Default release-channel policy: `ShareX/XerahS` = pre-release; `KovaForge/XerahS` = full latest release. Use `--set-prerelease` / `--no-prerelease` only for intentional overrides.

## Known iteration foot-guns

One line per failed release iteration. Read before tagging; append a line for every new failure.

- **Removing a shipped artifact leaves stale checks in several layers.** When a failed run names a missing file, grep the exact string across `build/` (including `build/ci/*.py` and the `.ps1` test fixtures), `src/desktop/app/XerahS.App/XerahS.App.csproj`, and `.github/workflows/`, then fix every hit in one `[Fix]` commit. Each run only reveals the first failing layer.
- v0.33.4: Chocolatey metadata sync used an anonymous GitHub API request and hit a shared runner rate limit (403). Pass the job token to the sync step and authenticate only the API metadata request; keep token-precedence regressions in the Windows job.
- v0.30.4: `build/windows/package-portable.ps1` still required `frontend/dist/index.html` from the removed ShareX.VideoEditor frontend.
- v0.30.5: `build/windows/test-package-portable.ps1` fixture held a second `frontend/dist/index.html` reference.
- v0.30.6: `XerahS.App.csproj` `<Error>` validators required the watch folder daemon/omaxerahs `.runtimeconfig.json` sidecar on macOS; .NET 10 single-file publish embeds it.
- v0.30.7: `build/macos/package-mac.sh` bundle validation required the same macOS runtimeconfig sidecar.
- v0.30.8: `.github/workflows/release-build-all-platforms.yml` post-build archive `grep -q` required the macOS runtimeconfig sidecar.
- v0.30.9: `build/ci/validate_release_assets.py` (the `release` job, after every build job passed) required `frontend/dist/index.html` in the portable ZIP and the macOS runtimeconfig sidecar; `build/ci/test_validate_release_assets.py` needed the same edit.
- v0.30.10: both RPM specs copied `usr/*` into `/usr/lib/xerahs/` (since 09-06). XerahS.Packaging's staged source already has a `usr/` layout, so it needs `cp -a usr/. %{buildroot}/usr/`; `repo-staging/xerahs.spec` consumes the flat release tarball, so it needs `cp -a . %{buildroot}/usr/lib/xerahs/`. The packager only logs "Skipped RPM package", so the Linux jobs passed and the `release` job failed on the missing `.rpm`. `Validate Linux archive` now fails the build job when the `.deb` or `.rpm` is missing.
- v0.31.0 (XIP0088, preventive): OmaSnap is an **optional** payload of the linux-x64 tarball only (`build-omasnap` Arch container job, `continue-on-error`). Every check of `omasnap/` files (`build/ci/validate_release_assets.py` `ensure_optional_omasnap`, the `Validate Linux archive` grep, `package-linux.sh` `append_optional_omasnap`) must stay conditional on the folder being present; never make it mandatory, and never add it to deb, rpm, AppImage or Flatpak without updating all of those layers together. When present, the binary and `licenses/LICENSE-MIT`, `LICENSE-OFL`, `LICENSE-ISC` are all required.
- A green build job does not mean every package was produced. Check the "Skipped ... package" lines in the Linux build log whenever the `release` job reports a missing asset.
- `run-release-sequence.sh` used to wait only 90 x 10 s (15 min) for the GitHub release, while a full run takes ~18-20 min (v0.30.10 and v0.30.11 both timed out with `Error: release vX.Y.Z was not found`). The wait is now 30 min by default (`--release-wait <minutes>` to change it). If it still times out, that message is not a CI failure: poll the run with `gh run view <id> --repo KovaForge/XerahS` until it completes, then apply Steps 6-7 manually.
- `--no-bump` skips syncing `build/windows/chocolatey/xerahs.nuspec` and the Flatpak metainfo `<release>` entry. When `Directory.Build.props` was already bumped by a feature commit, sync those two files yourself before tagging.
- v0.32.4: `--no-bump` with every version file already synced needs `--allow-empty`, or Step 3 stops with "no staged changes" before tagging (nothing is pushed).
- v0.32.4: Step 7 verified "latest" with `gh release view --json isLatest`, which gh 2.101 does not have; the release was already marked latest but the script exited 1. It now checks `gh api repos/<owner>/<repo>/releases/latest`.
- Several agents may share one checkout. Maintenance auto-commits whatever is uncommitted, so check `git status` and `git log` before a run to avoid releasing, or sweeping up, another agent's half-finished work.

## Notes (lessons learnt)

- Windows/PowerShell: bash may be unavailable; manual fallback must be first-class.
- Windows/PowerShell: avoid `if (git rev-parse <tag>)` for local tag existence checks; use `git show-ref --verify --quiet refs/tags/<tag>` and inspect `$LASTEXITCODE`.
- Build before bump: avoid tagging broken trees.
- Changelog: Step 2 blocks the bump until `docs/CHANGELOG.md` has the release version's section; it is skipped only when the file is absent or `--skip-changelog` is passed.
- Version sync: update every tracked `Directory.Build.props` with `<Version>`, sync `build/windows/chocolatey/xerahs.nuspec`, and prepend a new `<release version="X.Y.Z" date="YYYY-MM-DD">` entry to the `<releases>` block in `flatpak/com.xerahs.XerahS.metainfo.xml`. The metainfo insertion uses a generic "See CHANGELOG.md for details." placeholder description; edit it in the resulting commit before pushing if a hand-written note is preferred.
- **Windows packaging produces 6 assets per release**: EXE, MSI, and portable ZIP for each of x64 and ARM64. `build/windows/package-windows.ps1` uses the same self-contained publish payload for all three formats. Portable mode is enabled only in the ZIP; installer payloads must remain free of `portable.txt`.
- **ShareX.ImageEditor submodule must stay on `develop`**: after `git submodule update --init --recursive`, immediately run `git -C ShareX.ImageEditor fetch origin --prune`, `git -C ShareX.ImageEditor checkout develop`, and `git -C ShareX.ImageEditor pull --ff-only origin develop`. `git submodule update` checks out the parent-recorded commit and can leave a detached HEAD; do not proceed with release work until `git -C ShareX.ImageEditor status --short --branch` confirms `develop...origin/develop`.
- **WiX prerequisite (CI & local)**: use a pinned pre-v7 WiX CLI, currently `dotnet tool install --global wix --version 6.0.2` + `wix extension add --global WixToolset.UI.wixext/6.0.2`. The `release-build-all-platforms.yml` workflow installs WiX automatically in the `build-windows` job. For local MSI builds: install WiX first; if not present the script emits a warning and skips MSI.
- **MSI install layout**: per-user, no UAC elevation required. Binaries → `%LocalAppData%\Programs\XerahS\`; Plugins → `%USERPROFILE%\Documents\XerahS\Plugins\`; Start Menu shortcut created automatically.
- **Winget manifest**: both `InstallerType: nullsoft` (EXE) and `InstallerType: wix` (MSI) entries must be included for each architecture when submitting to winget-pkgs. See `build/windows/winget/manifests/0.16.0/ShareX.XerahS.yaml` as the template.
- Chocolatey asset naming: `build/windows/chocolatey/tools/chocolateyInstall.ps1` resolves `XerahS-<version>-win-x64.exe` or `XerahS-<version>-win-arm64.exe` from `ChocolateyPackageVersion`, so release bumps should not hardcode installer filenames there.
- Chocolatey checksums for community publication are post-release data because GitHub release assets do not exist until after the tag workflow completes. The tag workflow now performs that sync automatically for release packaging, and `build/windows/chocolatey/Sync-ChocolateyPackage.ps1` remains the manual fallback.
- Flatpak CI setup must fail loudly when the runtime cannot be installed; use `flatpak remote-add --no-gpg-verify` for unsigned Flathub setup, not `--no-sign-verify`.
- Flatpak manifest source paths are resolved relative to the manifest directory, so staging paths outside `flatpak/` need a `../` prefix.
- Flathub submission manifests must not depend on local `dist/xerahs-flatpak-staging`; generate a tag-pinned source-build candidate with `.ai/skills/publish-release/scripts/prepare-flathub-source-build.sh`.
- Flathub source-build candidates must include pinned submodule commits; GitHub source archives do not automatically include submodule contents.
- Flathub source-build candidates are not ready until NuGet/.NET dependency sources are generated and a network-disabled Flatpak build passes.
- Distro-repo candidates (PPA / COPR / OBS) wrap the existing GitHub linux tarball. They are not a second package format. `publish-distro-repos.sh` uploads when secrets are present and skips a backend when they are not.
- Flatpak build commands install into `/app`, not `/usr`; expose launchers through `/app/bin`.
- Flatpak build commands run from the module build directory, not the repository root; add icons, desktop files, metainfo, or other repository assets as explicit manifest sources before installing them.
- Flatpak `finish-args` must use options supported by `flatpak build-finish`; clipboard read/write flags are not valid `finish-args`.
- Flatpak session bus access is expressed as `--socket=session-bus` or narrower `--talk-name=` policies, not `--bus=session`.
- Flatpak CI bundling should export `flatpak-builder` output to an explicit local repo with `--repo=...`; validate files directly or use supported Flatpak commands, not `flatpak build-info`.
- Chocolatey release metadata lookup must use the active GitHub repository (`GITHUB_REPOSITORY`, `origin`, or explicit `-Repository owner/name`), not a hardcoded upstream owner.
- Chocolatey install scripts must also generate download URLs from the active release repository; updating only nuspec metadata is not enough.
- Dual-repo remote resolution must parse `git@github-<alias>:Owner/Repo.git`; bare `gh repo view` on a KovaForge fork checkout often returns `ShareX/XerahS` and must not be used for release targeting.
- After XIP0078 ad-hoc signing was added, macos-15 CI can fail when codesign hard-fails on unsigned nested managed DLLs; ad-hoc signing must use `--deep` and must not fail the release matrix for interim unsigned seals.
- Always verify the full required asset list on the chosen repo after workflow success; an empty GitHub release with zero assets is a failed publish even if a tag/release shell exists.
- Release channel is repo-scoped: never publish ShareX/XerahS as latest by default; never leave KovaForge/XerahS as pre-release by default after a successful publish-release run.
- Release reliability loop: tag push is not the end; monitor, fix, and retry until green.
