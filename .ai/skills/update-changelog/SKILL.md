---
name: update-changelog
description: Write or consolidate requested release notes in docs/CHANGELOG.md.
---

## Goal

`docs/CHANGELOG.md` is a **release notes** document for humans, not a git log.

Target style (canonical examples: `v0.22.236`, `v0.23.128`, `v0.23.129`):

- One **user-facing bullet** per component + intent cluster (often 3–8 bullets per category, not 50+).
- Plain language: what changed and why it matters.
- Platform prefixes when helpful: `**macOS — Hotkeys (XIP0078 P4)**:` or `**Linux — Clipboard (XIP0079 P3)**:`.
- Optional one-line release summary for large ranges (e.g. "Broad reliability release aggregating v0.23.27 onward").
- `---` horizontal rule between version sections.
- File opens with a `# Changelog` preamble (intro + semver legend), then newest versions.
- **No commit hashes** in bullets (version heading links to the tag when it exists).

The helper script produces a **draft**. An agent **must** rewrite that draft into release-note prose before publishing.

---

## Automation Script (Recommended)

Scripts:

- PowerShell (primary): `.ai/skills/update-changelog/scripts/update-changelog.ps1`
- Bash wrapper (macOS/Linux): `.ai/skills/update-changelog/scripts/update-changelog.sh`

Preview draft:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1
```

```bash
./.ai/skills/update-changelog/scripts/update-changelog.sh
```

Apply to `docs/CHANGELOG.md`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .ai/skills/update-changelog/scripts/update-changelog.ps1 -FromTag v0.23.128 -Version 0.23.129 -Apply
```

```bash
./.ai/skills/update-changelog/scripts/update-changelog.sh --from-tag v0.23.128 --version 0.23.129 --apply
```

Flags:

| Flag | Effect |
|------|--------|
| `-FromTag` / `--from-tag` | Lower bound (default: `git describe --tags --abbrev=0`) |
| `-Version` / `--version` | Target version (default: root `Directory.Build.props`) |
| `-Apply` / `--apply` | Upsert section into `docs/CHANGELOG.md` |
| `-OutputPath` / `--output-path` | Write draft to file |
| `-NoConsolidation` | Per-commit lines (debug only; do not publish) |
| `-IncludeHashes` | Append hashes (audit only; do not publish) |

**PowerShell script authoring**: use ASCII punctuation (colons, hyphens) in consolidation summary strings inside `update-changelog.ps1`. Unicode em dashes in `.ps1` string literals can break parsing on some hosts.

Include commit hashes only when explicitly requested for audit/debug work:

Script behavior (default):

1. **Skips noise commits** (hourly-review tracker/state, clawpatch ingest, bare version bumps, CI release-only commits).
2. **Buckets repetitive docs** (blog-draft series, XIP/IEIP proposal churn, hourly-review records).
3. **Buckets platform waves** (pipe-drain deadlocks, XIP P1–P5 platform work).
4. **Merges same category + component** into one bullet (semicolon-separated themes).
5. **Omits hashes** unless `-IncludeHashes`.
6. **Links version heading** only when `vX.Y.Z` tag exists locally or on `origin`.
7. **Appends `---`** after each generated version block.

---

## Mandatory Agent Rewrite Pass

After running the script (or when editing by hand), **always** compress further:

### 1. Collapse commit-log categories

If a `### Fixes` section still has 10+ `**Core**:` lines, merge into themed bullets:

```markdown
### Fixes
- **MCP server**: History search parsing, blob resource hardening, task identity race, stale-path diagnostics.
- **Linux**: Pipe-drain deadlocks across CLI tools, theme service, clipboard, input, and capture helpers; Oem102 hotkey mapping; Wayland active-window routing.
- **macOS**: Clipboard path whitespace, dock hide for tray startup, upload picker fallback, update prompts.
```

### 2. Roll up sparse patch versions

When several consecutive versions are release-only or trivial, combine:

```markdown
## v0.23.121 / v0.23.120

### Changed
- Release version bumps only; no additional user-facing changes in these ranges.
```

Or fold patch-only work into the next meaningful minor heading with a summary line.

### 3. Use platform + XIP labels for improvement-plan work

```markdown
### Features
- **macOS — App bundle (XIP0078 P1)**: Render `Info.plist` from template with stable bundle identity (`com.xerahs.app`).
- **Linux — Notifications (XIP0079 P2)**: After-upload toasts support action buttons via portal and `notify-send --action` fallback.
```

### 4. Drop or one-line internal-only work

Omit (or fold into Documentation as one line):

- Hourly review tracker / state JSON updates
- Individual blog-draft add/refresh commits
- Clawpatch report ingestion
- "Record X in tracker" commits

### 5. Target density

| Range | Target bullets per category |
|-------|----------------------------|
| Small patch (1–5 user commits) | 1–3 per category |
| Medium release (6–30 commits) | 3–8 per category |
| Large wave (30+ commits) | 5–12 per category; use sub-themes |

Aim for **50–90% fewer lines** than raw `git log` output.

---

## Draft → Final (agent rewrite)

Actions on every draft:

1. Turn commit subjects into **what changed for users**.
2. Fold internal build fixes into one line (e.g. Linux-only UI partials off macOS builds).
3. Collapse blog/XIP doc churn into one Documentation bullet or omit.
4. Split macOS (XIP0078) and Linux (XIP0079) into separate version sections when they shipped as different tags.

---

### Bad — commit log (never publish)

```markdown
### Fixes
- **Core**: LinuxCliToolRunner pipe-drain deadlock (b23cb6ba)
- **Core**: LinuxThemeService gsettings pipe-fill + timeout-stretching deadlock (74652cf4)
- **Core**: Update hourly review tracker for LinuxInputService xdotool fix (4c307ea0)
- **Core**: Add 2026-07-01 blog draft. (1489a3ae)
```

---

## Notes

- `-Version` defaults to root `Directory.Build.props`.
- `-FromTag` defaults to `git describe --tags --abbrev=0`.
- The script upserts the version heading for the target version (replaces existing linked or unlinked section for that version or inserts after `## Unreleased`).
- Link version headings only when the corresponding Git tag exists locally or on `origin`. Existing tag example: `## [v0.22.236](https://github.com/ShareX/XerahS/releases/tag/v0.22.236)`. Unreleased/no-tag example: `## v0.22.237`.
- Commit hashes are omitted by default to keep the changelog readable. Use `-IncludeHashes` only for temporary audit/debug drafts, not normal release notes.
- **Default consolidation**: `Get-ConsolidationBucket` in `scripts/update-changelog.ps1` merges commits that match the same similarity bucket (for example: **ShareX.ImageEditor**, **2026 blog** draft series including Refresh commits, **XIP/IEIP/KFIP** docs, **OpenClaw/CLI**, **MCP history**, **OCR onboarding**, **FFmpeg/media**, **Linux pipe-drain**, **settings/backup**, **hourly-review meta is skipped entirely**). Extend that function when new repetitive patterns appear.
- **Skip agent meta commits**: `Test-SkipChangelogCommit` excludes hourly review tracker/state updates, version-only bumps, and sweep/audit log commits from user-facing release notes. Do not re-add these during manual review.
- **Single unreleased heading**: fold duplicate prerelease sections (for example v0.23.105, v0.23.98) into one heading for the current `Directory.Build.props` version. Keep `# Changelog` and the intro block at the **top** of the file, not mid-file.
- Always **manually review** for wording, missed merges, and contributor attribution (`#PR`, `@user`) before publishing.

## Version Grouping Strategy

### Current unreleased work

- Lower bound: latest released tag (`git describe --tags --abbrev=0`).
- Upper bound: `HEAD`.
- Heading: root `Directory.Build.props` `<Version>`.
- One heading per target version unless user requests historical reconstruction.

### Historical / cleanup passes

When rebuilding noisy sections (as in v0.23.117 → v0.23.27 rollup):

1. Read commits across the whole range.
2. Group by **user-facing theme**, not by patch version.
3. Keep version headers for releases that matter; merge trivial patches.
4. Preserve `v0.22.236`+ summarized style for older stable releases.

---

## Categories

Use [Keep a Changelog](https://keepachangelog.com/) sections. The automation script consolidates via `Get-ConsolidationBucket` and skips agent meta via `Test-SkipChangelogCommit`; agents must still edit the draft. Target ~40–60 bullets for a typical unreleased range.

- **Features** — new user-visible capability
- **Fixes** — bug fixes and reliability hardening
- **Changed** — behavior/dependency changes that aren’t pure fixes
- **Refactor** — internal-only (omit unless user-facing)
- **Build** — packaging, dependencies, CI
- **Documentation** — docs, blog, XIPs (user-facing only)
- **Testing** — test infrastructure (usually omit unless major)
- **Performance** — measurable user-visible gains

Map commit prefixes:

| Commit prefix | Category |
|---------------|----------|
| `[Feature]` / `feat` | Features |
| `[Fix]` / `fix` | Fixes |
| `[Docs]` / `docs` | Documentation |
| `[CI]` / `build` / `chore(infra)` | Build |
| `[Refactor]` | Refactor |

---

## Attribution

- External contributors: `(#PR, @username)` on the relevant bullet.
- Do not attribute maintainer merge commits or internal agent sweeps.

---

## File Layout

```markdown
# Changelog

All notable changes to XerahS will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):

- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

---

## v0.23.129

### Features
- **Component**: User-facing description.

---

## v0.23.128
...
```

When applying updates, ensure the preamble exists at the top. Insert new version sections **after** the preamble `---` and **before** older versions.

---

## Workflow

1. Resolve range: `-FromTag` + `Directory.Build.props` version.
2. Run script → draft (preview, do not `-Apply` until reviewed).
3. **Rewrite draft** using mandatory agent pass (above).
4. Remove duplicate/orphan verbose blocks if consolidating history.
5. Ensure `---` between versions and preamble at top.
6. Run mojibake normalization (see below).
7. Verify: no hashes in bullets, linked headings only for existing tags, readable density.

### Mojibake cleanup (after any write)

```powershell
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$bytes = [System.IO.File]::ReadAllBytes('docs/CHANGELOG.md')
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    $bytes = $bytes[3..($bytes.Length - 1)]
}
$c = $utf8NoBom.GetString($bytes)
$c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x2014
$c = $c -replace [char]0x00C2 + [char]0x00A7, [char]0x00A7
$c = $c -replace "\r?\n", "`n"
$c = $c -replace "`n{3,}", "`n`n"
$c = $c -replace "`n", "`r`n"
[System.IO.File]::WriteAllText('docs/CHANGELOG.md', $c, $utf8NoBom)
```

Never use `[System.Text.Encoding]::UTF8` when writing Markdown. On Windows PowerShell that encoding emits a UTF-8 BOM and the Markdown Hygiene workflow fails.

---

## Consolidation Buckets (extend in script)

The script's `Get-ConsolidationBucket` merges repetitive patterns. Extend when new churn appears:

| Pattern | Merged summary |
|---------|----------------|
| `ShareX.ImageEditor` commits | ShareX.ImageEditor submodule updates |
| `2026-.. blog` add/refresh | Blog drafts (2026 series) |
| `XIP/IEIP` proposal docs | XIP/IEIP proposals and related documentation |
| `hourly review` / `tracker` / `clawpatch` | *(skipped — not user-facing)* |
| `pipe-drain` / `stderr` + Linux/macOS service | Platform pipe-drain deadlock hardening |
| `XIP0078` / `XIP0079` + `P\d` | Platform improvement-plan item (keep P number) |
| `multipart` / `S3 multipart` | Multipart upload support |
| `[CI] Release v` only | *(skipped)* |

---

## Related

- Release sequence: `.ai/skills/publish-release/SKILL.md` runs this skill as Step 2. Use the post-bump release version for `-Version` and the target repo's latest tag for `-FromTag`; `run-release-sequence.sh` aborts the bump if `docs/CHANGELOG.md` lacks that version's section.
- Maintenance: `.ai/skills/run-maintenance/SKILL.md`
