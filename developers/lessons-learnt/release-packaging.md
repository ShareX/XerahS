# Release packaging lessons

Background for [publish-release](../../.ai/skills/publish-release/SKILL.md). Read the entry for the subsystem you are changing (WiX, MSI, Chocolatey, Flatpak, Flathub, distro repos, macOS signing).

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
