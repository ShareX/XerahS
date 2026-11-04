# Manual release flow (no bash)

Use with [../SKILL.md](../SKILL.md) when `bash` is not in PATH.

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
   - Verify all **6 Windows assets** are attached (EXE, MSI, and `-portable.zip` for both `win-x64` and `win-arm64`) plus macOS and Linux assets. Validate portable ZIP contents as described in [references/windows-portable.md](windows-portable.md), not just filenames.

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
