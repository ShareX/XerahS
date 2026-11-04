---
name: port-imageeditor
description: Port ShareX.ImageEditor changes from a local ShareX checkout into the XerahS submodule when requested.
---

# Port ImageEditor: Local ShareX -> XerahS

Use this workflow whenever XerahS needs to catch up with the current `ShareX.ImageEditor`
state from the local ShareX repo.

## Source of truth

On the known workstations, do not clone ShareX again. The local ShareX checkout is the
upstream reference.

ShareX and XerahS are **not** always siblings. Resolve `$ShareXRepo` and `$XerahSRoot`
independently. The editor cwd is often `C:\WINDOWS\system32` or another unrelated folder —
never run `git` there.

### Session paths (resolve first)

User-specified paths win. Then probe, in order, until `git rev-parse --is-inside-work-tree`
succeeds.

`$ShareXRepo` (the ShareX git root, the directory that contains `ShareX.ImageEditor/`):

1. Path the user named (file or folder; if they named `...\ShareX Team\ShareX`, use that)
2. `C:\Users\Public\source\repos\ShareX Team\ShareX`
3. `C:\Users\liveu\source\repos\ShareX Team\ShareX`
4. `/Users/mike/Projects/ShareX Team/ShareX`
5. `/home/mike/Projects/ShareX/ShareX` (Linux workstation)

`$XerahSRoot` (the XerahS git root, the directory that contains the `ShareX.ImageEditor`
submodule):

1. Path the user named
2. `git rev-parse --show-toplevel` from any path under a XerahS checkout
3. `C:\Users\Public\source\repos\KovaForge\XerahS` (KovaForge workstation)
4. `C:\Users\Public\source\repos\ShareX Team\XerahS` (ShareX Team workstation)
5. `C:\Users\liveu\source\repos\ShareX Team\XerahS`
6. `/Users/mike/Projects/ShareX Team/XerahS`
7. `/home/mike/Projects/KovaForge/xerahs` (Linux workstation; lowercase folder name)

Both Public checkouts are first-class. Prefer the one the user named, otherwise the
one that exists and is a git work tree. Do not assume XerahS is a sibling of ShareX.

### Cloud/CI fallback (no local ShareX checkout)

When the skill runs on a host without any of the ShareX layouts above, the "do not clone"
rule does not apply. Instead:

1. Clone upstream once, blobless for speed:
   `git clone --filter=blob:none https://github.com/ShareX/ShareX.git /tmp/ShareX`
2. Use `/tmp/ShareX` as `$ShareXRepo`.
3. Use the current XerahS working tree as `$XerahSRoot`, and run
   `git submodule update --init ShareX.ImageEditor` first if the submodule directory is
   empty.
4. No base worktree is needed: `scripts/triage.py` reads the base and head versions
   straight from git objects (see step 2e-triage).

Linux hosts build the ImageEditor project, the desktop solution, and the test suite fine
(`EnableWindowsTargeting` is already configured), so all verification gates below apply
unchanged.

### Path map

| Role | Path |
|------|------|
| Upstream ShareX repo | `$ShareXRepo` |
| Upstream source tree | `$ShareXRepo/ShareX.ImageEditor` |
| XerahS root | `$XerahSRoot` |
| XerahS ImageEditor repo | `$XerahSRoot/ShareX.ImageEditor` |
| XerahS ImageEditor code root | `$XerahSRoot/ShareX.ImageEditor/src/ShareX.ImageEditor` |

All command examples use forward slashes. They work on Windows, macOS, and Linux for
`git -C`, `cd`, and `dotnet build`.

Set both roots once per session:

```powershell
# Windows PowerShell — example from a Public + KovaForge layout
$ShareXRepo = 'C:\Users\Public\source\repos\ShareX Team\ShareX'
$XerahSRoot = 'C:\Users\Public\source\repos\KovaForge\XerahS'
```

```bash
# macOS / Linux shell — sibling layout
ShareXRepo="/Users/mike/Projects/ShareX Team/ShareX"
XerahSRoot="/Users/mike/Projects/ShareX Team/XerahS"
```

## Core rules

1. The newest relevant upstream commit must be resolved from the local ShareX repo's git history, not guessed.
2. Diff against the mapped XerahS code root. The upstream source lives at `ShareX/ShareX.ImageEditor/...`; the target code lives at `XerahS/ShareX.ImageEditor/src/ShareX.ImageEditor/...`.
3. Scan every relevant upstream commit from the previous sync point through the newest relevant commit, not just the tip commit.
4. Before implementing anything, publish a concise implementation manifest that lists every bug fix and enhancement identified from the new ShareX commits.
5. For every manifest item, check whether XerahS already fixed it, implemented it differently, partially implemented it, or has a better local behavior that must be preserved.
6. Prefer the best combined design over a direct ShareX copy: preserve superior XerahS behavior, import superior ShareX behavior, and write a custom implementation when that is the cleanest integration.
7. Read the affected upstream and XerahS files to clarify intent, control flow, rendering behavior, and wiring before re-implementing anything ambiguous.
8. Preserve XerahS-only repository-level differences such as the submodule's `src/` layout, multi-targeting, and any confirmed host integration changes.
9. Do not overwrite XerahS-specific fixes blindly. If a target file already diverged for Avalonia or host integration, port the upstream intent instead of doing a raw replace.
10. Build before claiming completion.
11. Commit each completed bug fix and enhancement separately in the `ShareX.ImageEditor` submodule. Do not bundle unrelated manifest items into one port commit.
12. If verification passes and the user did not ask to pause, push the submodule commits and then commit and push the XerahS root pointer update.
13. Do not add a `ProjectReference` to `ShareX.Avalonia`, `ShareX.Avalonia.Tools`, or `ShareX.Tools`. XerahS ImageEditor is a standalone Avalonia/Skia library. Rewrite `ShareX.AvaloniaUI.*` usings and `avares://ShareX.Avalonia/...` URIs back to ImageEditor types (`Presentation.Theming`, `Presentation.Rendering`, `Hosting`).
14. Do not rename XerahS `Hosting/` / `AvaloniaIntegration` to upstream `Integration/` / `ImageEditorIntegration`. Port new host APIs into `Hosting/AvaloniaIntegration.cs`.
15. XerahS is English-only and must not gain multi-language support. Never port ShareX `Localization/` (default or satellite `.resx`, `Strings.Designer.cs`, `EffectBrowserLocalization.cs`, translation READMEs, `ValidateTranslations.ps1`) and never add resx generator / `EmbeddedResource` localization items to the XerahS ImageEditor csproj. When ShareX replaces UI text with `Strings.*` or `{x:Static res:Strings.*}`, keep or restore the English literal — take the wording from the default `Strings.resx` value if needed. Mark those upstream commits `skip` in the manifest. If `Localization/` is already in the XerahS submodule from a prior port, do not grow it: no new cultures, no resx syncs, no new `Strings.*` call sites.

## Step 0 - Resolve the upstream commit range

### 0a0 - Align the XerahS root and submodule safely

Before resolving the ShareX range, make sure the XerahS root branch and the
`ShareX.ImageEditor` submodule are in a predictable state.

```powershell
git -C "$XerahSRoot" status --short --branch
git -C "$XerahSRoot" submodule status
git -C "$XerahSRoot/ShareX.ImageEditor" status --short --branch
```

If the root branch is behind and there are no conflicting local changes, prefer a
fast-forward pull that does not recurse into submodules:

```powershell
$env:GIT_TERMINAL_PROMPT = '0'
git -C "$XerahSRoot" pull --ff-only --no-recurse-submodules
```

This avoids a root pull hanging on nested submodule fetches. If a previous pull or
fetch stalls with no lock held, stop only the stale `git` processes and retry the
root fast-forward with `--no-recurse-submodules`.

After the root is current, align the submodule branch with its remote before
porting. If a local submodule commit is a cherry-pick duplicate, let `git pull
--rebase` skip it rather than preserving a duplicate patch:

```powershell
$env:GIT_TERMINAL_PROMPT = '0'
git -C "$XerahSRoot/ShareX.ImageEditor" pull --rebase
```

Do not stage the root submodule pointer update that results from this housekeeping
until after the port build gates pass.

### 0a - Confirm the local ShareX checkout is current

```powershell
git -C "$ShareXRepo" status --short
git -C "$ShareXRepo" branch --show-current
git -C "$ShareXRepo" rev-parse HEAD
git -C "$ShareXRepo" fetch --prune
git -C "$ShareXRepo" status --short --branch
```

Use the checked-out branch as the default upstream branch unless the user requests a different ref.

If the local ShareX checkout is behind its upstream tracking branch, pull it before resolving the ImageEditor range:

```powershell
git -C "$ShareXRepo" pull --ff-only
```

If ShareX has local uncommitted changes, do not overwrite them. Prefer:
- If the changes are unrelated and the checkout is only needed for read-only upstream assessment, use `git pull --rebase --autostash` so the newest source is available locally.
- If the changes conflict with the pull or look relevant to `ShareX.ImageEditor`, stop and report that the local ShareX checkout must be cleaned or reviewed before porting.

Local checkouts may be **shallow** (the Linux workstation's is a depth-1 clone). Check
before resolving the range; a shallow clone makes `<last_sync>..HEAD` fail with
`bad revision`:

```bash
git -C "$ShareXRepo" rev-parse --is-shallow-repository
# if true, deepen past the last sync date from PORT_STATUS.md (much cheaper than --unshallow)
git -C "$ShareXRepo" fetch --shallow-since=<a date before the last sync> origin <branch>
git -C "$ShareXRepo" cat-file -t <last_synced_sharex_hash>   # must print "commit"
```

After pulling, record the updated ShareX `HEAD` and use that local source for the rest of the assessment. This keeps the upstream code local and fast to inspect without cloning ShareX again.

### 0b - Find the latest ShareX commit that touches `ShareX.ImageEditor`

```powershell
git -C "$ShareXRepo" `
  log -1 --format="%H %cs %s" -- ShareX.ImageEditor
```

This is the latest relevant upstream commit. Record it.

### 0c - Find the last recorded sync point in XerahS

Read `$XerahSRoot/ShareX.ImageEditor/PORT_STATUS.md`.

Expected fields:
- `ShareX.ImageEditor commit: <hash>`
- `XerahS submodule last synced to: <hash>`

If the file is missing or stale, derive the baseline from repo history and note the assumption in the final update.

### 0d - List pending upstream commits

```powershell
git -C "$ShareXRepo" `
  log --reverse --oneline <last_synced_sharex_hash>..HEAD -- ShareX.ImageEditor
```

Use this list to decide whether the catch-up is:
- Low risk: isolated bug fix in a small file
- Medium risk: touches controllers, view models, or multiple files
- High risk: adds files, changes tooling or rendering, or updates editor interaction behavior

Do not treat this commit list as a queue for blind cherry-picks. Use it as a review list for semantic porting.
Read the whole queue from oldest to newest so you understand how fixes and features build on each other.

## Step 1 - Map source paths to target paths

The ShareX tree and XerahS submodule do not have the same repository layout.

| Upstream path | Target path |
|---------------|-------------|
| `ShareX.ImageEditor/Assets/...` | `ShareX.ImageEditor/src/ShareX.ImageEditor/Assets/...` |
| `ShareX.ImageEditor/Core/...` | `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/...` |
| `ShareX.ImageEditor/Hosting/...` | `ShareX.ImageEditor/src/ShareX.ImageEditor/Hosting/...` |
| `ShareX.ImageEditor/Integration/...` | `ShareX.ImageEditor/src/ShareX.ImageEditor/Hosting/...` (keep XerahS folder name; `ImageEditorIntegration.cs` maps to `AvaloniaIntegration.cs`) |
| `ShareX.ImageEditor/Localization/...` | **Skip.** XerahS is English-only (core rule 15). Do not copy this tree. |
| `ShareX.ImageEditor/Presentation/...` | `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/...` |
| `ShareX.ImageEditor/ShareX.ImageEditor.csproj` | `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj` |

Do not diff the upstream folder against the submodule repo root. Always diff it against
`src/ShareX.ImageEditor`.

## Step 2 - Inspect the exact upstream delta

### 2a - List files changed since the last sync

```powershell
git -C "$ShareXRepo" `
  diff --name-only <last_synced_sharex_hash>..HEAD -- ShareX.ImageEditor
```

### 2b - Review each pending commit with stats

```powershell
git -C "$ShareXRepo" `
  show --stat --summary --oneline <sharex_commit>
```

Also inspect the actual patch for behavior-critical commits:

```powershell
git -C "$ShareXRepo" `
  show <sharex_commit> -- ShareX.ImageEditor
```

For a real catch-up, do this for every commit in the pending range from the last synced hash to the newest relevant hash.
Summarize for yourself:
- which user-visible features were added
- which bugs were fixed
- which commits depend on earlier commits in the range
- which files are the authoritative implementation points

### 2c - Publish the implementation manifest before editing

Before changing files, post a concise manifest in chat. This is a mandatory gate for the port session.

The manifest must include every bug fix and enhancement identified in the pending ShareX commits, grouped by implementation item rather than by raw file. Each item must include:
- Type: `Bug fix`, `Enhancement`, `Refactor with behavior impact`, or `Infrastructure`
- Source commit(s): the ShareX hash and short subject
- Upstream behavior: what changed for users or editor internals
- XerahS status: `missing`, `already fixed`, `implemented differently`, `partially implemented`, or `conflicts with XerahS`
- Target files: the mapped XerahS files expected to change
- Decision: raw sync, manual merge, custom implementation, keep XerahS behavior, or intentional skip with reason
- Rationale: why that decision gives XerahS the best behavior
- Commit plan: the intended standalone submodule commit subject for this item, or `none` when the item is kept/skipped

Use this shape:

```markdown
## ImageEditor Port Manifest

- [ ] Bug fix: <short name>
  Source: `<hash>` <subject>
  Upstream behavior: <one sentence>
  XerahS status: <missing/already fixed/implemented differently/partially implemented/conflicts>
  Target files: `<mapped/file.cs>`, `<mapped/file.axaml>`
  Decision: <raw sync/manual merge/custom implementation/keep XerahS/skip>
  Rationale: <why this is the best XerahS outcome>
  Commit plan: `[ShareX.ImageEditor] [Fix] <short description> from ShareX@<hash>`

- [ ] Enhancement: <short name>
  Source: `<hash>` <subject>
  Upstream behavior: <one sentence>
  XerahS status: <missing/already fixed/implemented differently/partially implemented/conflicts>
  Target files: `<mapped/file.cs>`
  Decision: <raw sync/manual merge/custom implementation/keep XerahS/skip>
  Rationale: <why this is the best XerahS outcome>
  Commit plan: `[ShareX.ImageEditor] [Enhancement] <short description> from ShareX@<hash>`
```

During implementation, work against this manifest. Announce the item being implemented before editing it, and update the item status as it is completed or intentionally skipped.

Do not use `raw sync` as the default. It is only acceptable after comparing the mapped XerahS file and confirming there is no local fix, no different design, and no host-specific behavior worth preserving.

When multiple manifest items share prerequisite scaffolding, such as a new enum, helper, or control required by later items, either:
- include the shared scaffold in the first item that needs it when that keeps the commit coherent, or
- create a separate preparatory commit with `[ShareX.ImageEditor] [Infrastructure] ...` before the dependent bug-fix/enhancement commits.

Do not create one large "sync latest ShareX changes" submodule commit unless the pending range has exactly one cohesive manifest item.

Grouping is acceptable when the range has 30+ ImageEditor commits, 50+ changed files, or
when central files (`MainViewModel.cs`, `EditorCore.cs`, `EditorView.axaml`,
`AnnotationToolbar.axaml`) are touched by many items at once. Commit each cleanly
file-scoped feature (new partial-class features, `EditorBuiltInToolbars`, asset syncs)
separately, and land the interleaved central files in one final `[Port]` commit that
lists the covered items in its body. Note in `PORT_STATUS.md` that intermediate commits
only build as a batch. Prefer the manifest to still list every item individually even
when commits are grouped. Do not create a localization-catalog commit (core rule 15).

### 2d - Read code to remove ambiguity

If the commit message or patch alone is not enough, read the upstream implementation files and the current XerahS counterparts before editing.

Typical files to inspect:
- controllers
- view models
- rendering and visual factory code
- annotation model classes
- views and control markup
- `Strings.*` / `{x:Static res:Strings.*}` call sites (restore English literals; do not copy `Localization/`)
- `Hosting/AvaloniaIntegration.cs` vs upstream `Integration/ImageEditorIntegration.cs`
- the target `.csproj` for new files or assets (never for resx generator entries)

### 2e - Compare mapped files, not raw repo roots

For each changed upstream file `ShareX.ImageEditor/<relative_path>` compare it to:
`$XerahSRoot/ShareX.ImageEditor/src/ShareX.ImageEditor/<relative_path>`.

If the target file does not exist, it is a net-new addition and therefore high risk.
`Integration/*` that already exists under XerahS `Hosting/` is a rename, not NEW.

### 2e-triage - Automate divergence triage for large ranges

For ranges with dozens of changed files, do not eyeball each file. Run the bundled
classifier (Python 3, no extra packages; works on Windows, macOS, and Linux):

```bash
python3 .ai/skills/port-imageeditor/scripts/triage.py "$ShareXRepo" "$XerahSRoot" <last_sync> \
  --diff-dir <scratch>/triage
```

It reads upstream base/head from git objects (no worktree), maps `Integration/` to
`Hosting/`, applies the XerahS namespace rewrites (`REWRITES` in the script:
`ShareX.AvaloniaUI.Theming` -> `Presentation.Theming`, the lucide `avares://` URI,
`ImageEditorIntegration` -> `AvaloniaIntegration`), ignores header/BOM/EOL/blank-line and
`using`-order noise, and writes `base-xerahs` / `base-head` diffs for every file that
needs a human.

Categories:

- `NEW`: absent from the XerahS code root — sync it in as a new file, except
  `Integration/*` (map to `Hosting/`), `Localization/*` (skip, core rule 15), and files
  that exist only because ShareX extracted them into `ShareX.Avalonia`.
- `SAFE_SYNC`: the XerahS file equals the upstream *base* version after normalization and
  rewrites — syncing upstream HEAD is safe because XerahS never diverged in content.
  Never `SAFE_SYNC` `Localization/`.
- `NO_OP`: upstream base equals head after normalization and rewrites (for example a
  `using` move to `ShareX.AvaloniaUI.Theming`). Nothing to port.
- `ALREADY`: XerahS already matches upstream head.
- `UP_DELETED`: upstream deleted the file; keeping or deleting it is a manifest decision.
- `AVALONIA_NS`: the upstream delta only touches `using`/`xmlns` lines the rewrites do
  not cover (usually removing a `using`). Keep XerahS; but see the `BitmapConversionHelpers`
  note in 3b — a removed `using` can mean the type moved out of ImageEditor upstream.
- `SKIP_I18N`: anything under `Localization/`. Skip; do not treat as NEW or SAFE_SYNC.
- `DIVERGED`: real content differences — these carry XerahS adaptations and require a
  manual merge.

For each `DIVERGED` file, produce two normalized diffs: `base -> xerahs` (the XerahS
adaptation to preserve) and `base -> head` (the upstream change to port). Files whose
`base -> xerahs` delta is only blank lines or an encoding artifact can be treated as
`SAFE_SYNC`.

### 2e-sync - Apply the mechanical part with `sync.py`

After the manifest is posted, apply SAFE_SYNC, NEW, and DIVERGED files in one pass:

```bash
python3 .ai/skills/port-imageeditor/scripts/sync.py "$ShareXRepo" "$XerahSRoot" <last_sync> --dry-run
python3 .ai/skills/port-imageeditor/scripts/sync.py "$ShareXRepo" "$XerahSRoot" <last_sync>
```

- SAFE_SYNC/NEW files are written from upstream head with the rewrites applied, the
  target's header state preserved (new `.cs` files get the submodule header), UTF-8
  without BOM, and LF endings (policy below).
- DIVERGED files are 3-way merged (`git merge-file`, base = last sync) so XerahS
  adaptations and upstream changes combine automatically; overlapping hunks are left as
  `<<<<<<< xerahs` / `>>>>>>> upstream` markers. Resolve every marker, then `grep -rn '<<<<<<<' src` must be empty.
- `KEPT_TYPES` in the script re-adds `using` lines for types ShareX moved out of
  ImageEditor but XerahS keeps (currently `BitmapConversionHelpers`).
- The script refuses to run over uncommitted `src/` changes, and only `--force`
  overrides that. Run it once per range, before any manual edits.

Merge output still needs review:
- **Misaligned hunks.** When a conflict's XerahS side ends mid-method, or the resolved
  file will not compile, rebuild that file from `git show HEAD:<file>` and apply the
  upstream intent by hand.
- **Removals ported by accident.** A clean merge still applies upstream removals you
  decided to keep. Reverse-apply the removing commit for just those files:
  `git -C "$ShareXRepo" show <hash> -- <files> | sed 's#\([ab]\)/ShareX.ImageEditor/#\1/src/ShareX.ImageEditor/#g' > p.patch`
  then run `git apply -R --recount -C1 p.patch` inside the submodule.
- **API moved between intermediate commits.** The base-to-head diff hides API that was
  added and then used within the range (for example `Annotation.TransformAdditionalPoints`
  and `MoveBy`, both new in this range). Read the per-commit patches when a merged override
  refers to a base member that XerahS lacks.

### 2e-headers - Header, BOM, and EOL policy during syncs

Upstream ShareX files carry a UTF-8 BOM, CRLF line endings, and the ShareX license
header. The XerahS submodule stores UTF-8 without BOM, LF endings, and a mix of the
`ShareX.ImageEditor - The UI-agnostic Editor library for ShareX` header and headerless
files. When syncing:

- Strip the BOM and convert CRLF to LF.
- For existing files, preserve the target file's current header state byte-for-byte
  (including "no header") and replace only the body below it.
- For new `.cs` files, add the submodule-standard `ShareX.ImageEditor` header wording.
- Never let a raw copy replace the XerahS header with the upstream ShareX wording.

### 2f - Compare behavior, not just text

For every manifest item, answer these questions before editing:

- Is the same bug already fixed in XerahS under a different implementation?
- Is the ShareX fix better, worse, or complementary to the XerahS behavior?
- Does XerahS have host integration, Avalonia-specific behavior, tests, or persistence hooks that ShareX does not have?
- Can the best result be achieved by combining both implementations instead of copying either one wholesale?
- Is a small custom implementation clearer than copying the upstream patch?

If XerahS already has an equal or better implementation, keep it and record the item as `keep XerahS behavior` or `skip`.
If both implementations solve different parts of the problem, create a manual merge or custom implementation and record the rationale in `PORT_STATUS.md`.

## Step 3 - Port or sync safely

### 3a - When a raw file sync is acceptable

Raw file sync is an exception, not the normal path. You may replace the target file with the upstream version only when all of these are true:
- The file lives under `Core/`, `Presentation/`, `Hosting/`, or `Assets/` and maps cleanly into `src/ShareX.ImageEditor` (never `Localization/`)
- The target file was compared and does not contain a relevant XerahS fix, alternate implementation, host integration, persistence hook, test-supported behavior, or Avalonia-specific adaptation that would be lost
- The upstream change is exactly what XerahS needs and there is no repo-layout-only difference inside the file
- The manifest item explicitly marks the decision as `raw sync` with a short rationale

### 3b - When manual porting is required

Port the intent instead of copying the whole file when any of these are true:
- The target file contains XerahS-specific Avalonia, SkiaSharp, or host wiring that is not present upstream
- The target file already diverged beyond the pending upstream commit range
- The upstream file assumes repository or project settings that do not match the submodule
- The upstream commit adds a feature partially present in XerahS and a direct replace would regress local behavior
- XerahS already has a different fix for the same bug and the best result needs parts of both implementations
- The upstream code is correct for ShareX but a custom XerahS implementation would be simpler, safer, or better aligned with current XerahS architecture

Manual porting usually means:
- keep the XerahS file as the base
- apply the upstream behavior in small, reviewable hunks
- re-implement the bug fix or feature after understanding the whole upstream flow
- keep any superior XerahS behavior and merge only the missing upstream behavior
- add or update tests for bugs where the upstream fix differs from the XerahS implementation
- rebuild after behavior-critical controller, view model, rendering, or view changes
- only replace the whole file when the diff is layout-only and no XerahS adaptation would be lost

When a transformed upstream patch cannot apply because of expected XerahS
divergence, a mapped final-state sync is acceptable as an implementation aid, but
only after the manifest has been posted. After such a sync, immediately re-apply
and verify these known XerahS adaptations before building:

- `Annotation.cs`: keep `JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")`,
  `JsonIgnore` on computed/runtime state, and XerahS `StepTailStyle`; add any new
  upstream `JsonDerivedType` entries such as cursor annotations.
- `BaseEffectAnnotation.cs` / `ImageAnnotation.cs`: keep `[JsonIgnore]` on the runtime
  bitmap properties (`EffectBitmap`, `ImageBitmap`) and the
  `System.Text.Json.Serialization` using.
- `EditorCore.cs`: keep `GetAnnotationsSnapshotForPersistence()`,
  `RestoreAnnotations(...)`, and number-counter resync after restore/renumbering.
- `MainViewModel.cs`: keep `ApplicationName` and `EditorTitle` because XerahS host
  windows bind to that title contract. `BuildWindowTitle` must use `EditorTitle`,
  not a ShareX product title string (that wording says "ShareX").
- `MainViewModel.ImageState.cs`: keep `CreateSourceImageCopyForPersistence()` and
  `GetAnnotationSnapshotForPersistence()`.
- `EditorView.CoreBridge.cs`: keep `GetAnnotationSnapshot()` and
  `RestoreAnnotations(...)` (with number-counter and history-state resync).
- `AvaloniaIntegration.cs`: preserve XerahS task-mode behavior, especially
  `ShowFileMenu = !taskMode` and start-screen suppression, while adding new
  upstream event wiring.
- `EditorIcons.cs`: preserve XerahS-only icon constants such as tail-style icons
  (`TailStyleTriangle`, `TailStyleArrow`) when upstream icon syncs replace the file.
- `NumberAnnotation.cs` / `StepControl.cs`: XerahS has the tail-style system
  (`StepTailStyle` Triangle/Arrow, `TryGetArrowTailOutline`,
  `TryGetCircleSegmentExitPoint`, arrow tail rendering). Merge upstream step
  features (StepType, IsBold, TailEnabled, tail geometry changes) into that system
  instead of replacing it; upstream deletes `TryGetCircleSegmentExitPoint`, but the
  XerahS arrow tail still needs it.
- Theming file names: keep XerahS `ImageEditorStyles.axaml` / `ImageEditorTheme.axaml`.
  Upstream has used `AppStyles`/`AppTheme` and later `EditorStyles` / Avalonia
  `ShareXTheme`. Rewrite those URIs back to the XerahS names. Root `XerahS.UI`
  (MainWindow resources), `XerahS.RegionCapture`, and `EditorView.axaml` StyleInclude
  all bind to the XerahS URIs. `XerahS.UI` `ThemeService` already calls
  `ThemeManager.SetTheme`; do not delete the standalone editor theme panel unless
  the user asks.
- `EditorView.axaml`: keep the XerahS
  `<StyleInclude Source="avares://ShareX.ImageEditor/Presentation/Theming/ImageEditorStyles.axaml"/>`
  block. Upstream applies styles app-wide from `AvaloniaIntegration.Initialize()`,
  which does not cover XerahS embedded hosting (MainWindow editor tab, RegionCapture
  overlay).
- `EffectBrowserPanel.axaml`: keep the XerahS-only StyleInclude added by submodule
  commit `72ff989` (tile spacing in standalone hosts).
- Root integration: when `IAnnotationToolbarAdapter` gains members, update
  `src/desktop/app/XerahS.RegionCapture/ViewModels/RegionCaptureAnnotationViewModel.cs`
  in the same session. RegionCapture is not in the submodule, but the full XerahS
  build depends on that adapter matching the submodule interface. Note:
  `XerahS.RegionCapture` declares its own `BorderStyle` enum in the parent
  namespace, which wins simple-name lookup over a `using BorderStyle = ...` alias;
  use a distinctly named alias such as `AnnotationBorderStyle`.
- Root integration: when `MainViewModel` event signatures change (for example the
  2026-07 move to `Func<Task>` `CopyRequested` and `Func<Task<string?>>`
  `SaveRequested`/`SaveAsRequested`), update
  `src/desktop/app/XerahS.UI/Services/MainViewModelHelper.cs` wiring in the same
  session, returning the saved path so editor notifications can display it.
- Root sweep for removed members: when upstream removes API (for example
  `TextUnderline` / `IsUnderline` in the 2026-07 range), grep `src/` and `tests/`
  for the removed names; `OverlayWindow.Canvas.cs` and
  `tests/XerahS.Tests/RegionCapture/*` are common consumers.
- Package additions: when the upstream csproj gains `PackageReference` items, add
  the versions to the **submodule's own** `ShareX.ImageEditor/Directory.Packages.props`
  (nearest `Directory.Packages.props` wins for CPM), not the XerahS root props. The
  XerahS root props intentionally disable central management for the submodule path.
  Do not drop `Microsoft.ML.OnnxRuntime.DirectML` / `Vortice.DXGI` while XerahS still
  ships background-remover in the submodule.
- Localization / i18n: skip the entire ShareX `Localization/` tree (core rule 15).
  Port English UI wording only. Do not replay the add-then-delete `EmojiCatalog_*`
  experiment.
- `AnnotationToolbar.ShowToolOptions`: XerahS `IAnnotationToolbarAdapter` already
  has `ShowToolOptions` meaning "current tool has option widgets." Name the host
  chrome override `ShowToolOptionsPanel` (or bind the control property only with
  `ElementName`).
- `EditorView.UseBuiltInToolbars`: default **true**. XerahS hosts do `new EditorView()`
  and `EditorWindow` without setting the flag. Workspace embeds can set it false.
- Tool windows extracted upstream (combiner, hash, QR, video, background remover,
  comparer, icon converter, screen color picker): keep them in ImageEditor unless
  XerahS.UI already owns that tool **and** the standalone `ShareX.ImageEditor.sln`
  app no longer needs the `AvaloniaIntegration.Show*` entry point. Combiner already
  lives in `XerahS.UI` — do not add it back to the submodule.
- RegionCapture: XerahS is N overlay windows + `RegionCaptureAnnotationViewModel` +
  a local `EditorCore`, not ShareX's embedded `EditorView`. Port annotation/core
  behavior (for example `SmartEraserAnnotation.ConfigureFill`) into
  `OverlayWindow.Canvas.cs` in the same session. Do not port `RegionCaptureWindow`.
- `BitmapConversionHelpers`: upstream moved it to `ShareX.AvaloniaUI.Imaging` and dropped
  `using ShareX.ImageEditor.Presentation.Rendering;` from its callers. XerahS keeps it
  in `Presentation.Rendering`, so synced callers need that `using` back (`sync.py`
  handles this via `KEPT_TYPES`).
- Editor Options panel (`EditorOptionsPanel`, `MainViewModel.EditorOptions.cs`,
  `ShowOptionsButton`, `OpenOptionsPanelCommand`): upstream removed it in `f58d576`
  (settings moved into the ShareX app). XerahS keeps it for the standalone editor
  (`AvaloniaIntegration` sets `ShowOptionsButton = true`). Keep the adapter members, the
  toolbar menu item, and the `OpenOptionsPanelRequested` wiring in
  `EditorView.Subscriptions.cs`.
- `EditorView` workspace-host APIs (`ConfigureForFullscreenWorkspace`,
  `LoadWorkspaceImage`, `InsertWorkspaceImageAnnotation`, `ResetModalContentPosition`, …)
  serve ShareX's embedded RegionCapture and are intentionally absent in XerahS. Port only
  `DisposeWorkspace` / `ReleaseAnnotationDisplayBitmaps` (used by `EditorWindow.OnClosed`),
  and unsubscribe `ThemeManager.ThemeChanged` in `OnUnloaded` and `DisposeWorkspace`.
- Win32-only helpers (`DllImport("user32.dll")` cursor-screen math, GDI magnifier):
  re-implement with Avalonia `TopLevel.Screens` / a cross-platform fallback. Do not
  copy the P/Invoke.

PowerShell versions in this repo may not support `Set-Content -Encoding
utf8NoBOM`. For mechanical header normalization during mapped syncs, use
`[System.Text.UTF8Encoding]::new($false)` with `[System.IO.File]::WriteAllText(...)`
instead of relying on that encoding name.

### 3c - When to write a custom implementation

Write a custom implementation when the upstream patch exposes the desired behavior but the XerahS architecture calls for a different shape.

Common cases:
- XerahS already has a related service, adapter, or persistence hook that should own the behavior.
- ShareX solves the bug in UI code, but XerahS should solve it in `EditorCore` or a shared controller.
- The upstream change duplicates logic already centralized in XerahS.
- The upstream behavior is useful, but its exact code would regress RegionCapture, embedded editor mode, tests, serialization, or host integration.
- Combining both implementations would otherwise create duplicated state or unclear ownership.

Record custom implementations in `PORT_STATUS.md` with the source ShareX commit, the XerahS files changed, and the reason for not copying upstream code.

### 3d - Preserve known XerahS repository-level differences

Keep these unless the user explicitly asks to change them:
- `src/ShareX.ImageEditor` repository layout
- `Hosting/` + `AvaloniaIntegration` (not `Integration/` + `ImageEditorIntegration`)
- Lucide, cursor `.cur` files, `ThemeManager`, `CursorAssetLoader`, and
  `BitmapConversionHelpers` inside the submodule
- Tool windows still hosted by `AvaloniaIntegration.Show*`
- XerahS-specific solution or project structure
- XerahS multi-targeting or packaging differences
- Any host integration already verified in XerahS
- English-only UI: do not import ShareX multi-language resources (core rule 15)

### 3e - New-file checklist

For each new upstream file:
1. If it lives under `Localization/`, skip it (core rule 15).
2. Create the mapped target directory if needed.
3. Add the file under `src/ShareX.ImageEditor`.
4. Update the target `.csproj` only if the new file requires an explicit item entry.
   Do not add `PublicResXFileCodeGenerator` or other resx generator items.
5. Search for references to the new type or view and port the wiring in the same session.

## Step 4 - Verification gates

### 4a - Targeted ImageEditor build

```powershell
cd "$XerahSRoot"
dotnet build "ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj" -m:1
```

If it genuinely stalls, stop it, clear the lock, and retry with single-node compilation when appropriate.

### 4b - Full solution build

```powershell
cd "$XerahSRoot"
dotnet build "src/desktop/XerahS.sln" -m:1
```

This must finish with 0 errors before any push.

### 4c - Standalone submodule solution and tests

The submodule ships its own solution with a standalone app project that consumes
`AvaloniaIntegration`; API changes that pass the XerahS build can still break it:

```powershell
cd "$XerahSRoot/ShareX.ImageEditor"
dotnet build "ShareX.ImageEditor.sln" -m:1
```

Also run the XerahS test suite; RegionCapture adapter and editor behavior tests live
there:

```powershell
cd "$XerahSRoot"
dotnet test "tests/XerahS.Tests/XerahS.Tests.csproj" -m:1
```

On Windows, Linux/macOS tests that start `/bin/sh` or assert POSIX/`file:///` path
shapes can fail without any ImageEditor change. To tell whether a failure is
pre-existing, run `git -C ShareX.ImageEditor stash -u`, rebuild the test project,
rerun the tests, then `stash pop`. A failure that reproduces without the port does not
block the port. Still report it, and fix it when the user asks.

## Step 5 - Update tracking

After the catch-up:
1. Update `$XerahSRoot/ShareX.ImageEditor/PORT_STATUS.md`
2. Record:
   - latest upstream ShareX commit used
   - previous recorded sync point
   - files added or updated
   - risk summary
   - adaptations kept for XerahS

Suggested status block:

```markdown
## Port Activity (<YYYY-MM-DD>)

- Previous recorded ShareX sync: `<old_hash>`
- Latest upstream ShareX commit touching ShareX.ImageEditor: `<new_hash>`
- Result: `Caught up through <new_hash>`
- Notes: `<manual adaptations or intentional skips>`
```

## Step 6 - Commit discipline

The submodule is a shared library repo, so submodule commits do not use the XerahS version prefix.

Commit each completed manifest item separately in the `ShareX.ImageEditor` submodule.

Use these submodule commit formats:

```text
[ShareX.ImageEditor] [Fix] <bug fix description> from ShareX@<hash>
[ShareX.ImageEditor] [Enhancement] <feature description> from ShareX@<hash>
[ShareX.ImageEditor] [Infrastructure] <shared prerequisite description>
[ShareX.ImageEditor] [Port] <description> from ShareX@<hash>
```

Use `[Port]` only for cohesive changes that are not cleanly a fix or enhancement, such as a behavior-impacting refactor. If one upstream commit contains several unrelated user-visible fixes or enhancements, split the XerahS commits by manifest item, not by upstream commit.

Before each submodule commit:
1. Stage only the files/hunks for that manifest item.
2. Run the smallest relevant build or test if the item is behavior-critical and fast enough.
3. Commit with the planned subject.
4. Continue to the next manifest item.

If a later item needs to amend a previous item because of a discovered integration issue, prefer a new targeted follow-up commit unless history has not been pushed and the scope is still clearly the same manifest item.

Then update the XerahS root repo to point to the new submodule commit.

## Step 7 - Push discipline

After verification succeeds:

1. Confirm every implemented manifest item has its own `ShareX.ImageEditor` submodule commit, except items explicitly kept or skipped.
2. Push the submodule branch after the full verification gates pass.
3. Stage the updated submodule pointer and any root tracking or skill changes in `XerahS`.
4. Commit the XerahS root repo using the next unreleased XerahS version prefix. The root commit may summarize the batch because it only records the final submodule pointer and host/test integration.
5. Push the XerahS root branch.

Do not stop after a local commit unless the user explicitly asks to pause before push.

## Fast path for this repo

For the common "catch up XerahS to the latest local ShareX state" task, run Steps 0 to 7 in order: resolve both roots, read the last sync hash from `PORT_STATUS.md`, update and diff the ShareX checkout, run `scripts/triage.py`, post the manifest, run `scripts/sync.py` (dry run first), commit each item in the submodule, build, update `PORT_STATUS.md`, then push the submodule and the root pointer.
