# Open issue audit — 2026-10-19

Target: `KovaForge/XerahS`, branch `develop`, starting at `2883d0d3`.
Source: all ten open issues and their comments at <https://github.com/ShareX/XerahS/issues>, fetched 2026-10-19.

## Priority and implementation plan

| Priority | Issue | Initial finding | Plan / completion evidence |
|---|---|---|---|
| P0 | [#296](https://github.com/ShareX/XerahS/issues/296) startup crash on Linux Mint | Unfixed: clipboard viewer calls `Show(owner)` for an invisible tray owner. | Show independently when the owner is invisible; exercise tray startup and background opens in headless UI tests. |
| P1 | [#294](https://github.com/ShareX/XerahS/issues/294) unsolicited background network probes | Unfixed: startup unconditionally calls `EnsureStarted()`. | Make background monitoring opt-in, expose the setting and start/stop control, verify disabled startup sends no probes. |
| P1 | [#293](https://github.com/ShareX/XerahS/issues/293) Linux logout blocked | Unfixed: managed environment cleanup does not opt out in Avalonia's X11 options. | Disable XSMP in X11 options and clear the native environment; test startup policy. |
| P1 | [#298](https://github.com/ShareX/XerahS/issues/298) editor swaps red/blue on macOS | Unfixed in pinned ImageEditor: both input branches assume BGRA. | Use actual pixel/alpha formats in both directions; test RGBA, BGRA, and alpha round trips; commit the library fix and its XerahS integration. |
| P1 | [#290](https://github.com/ShareX/XerahS/issues/290) stalled Wayland recording yields corrupt MP4 and success | Unfixed aspects remain: unfragmented `mp4mux`, forced stop without output validity checks. | Preserve playable partial output, make shutdown bounded and observable, reject unusable recordings, and add recording/finalization regressions. |
| P1 | [#288](https://github.com/ShareX/XerahS/issues/288) Windows crash leaves cursor invisible | Cursor recovery implemented in `46c1ac1f`; underlying crash needs attached-log audit. | Check recovery guard, failure paths, and report attachments before determining whether another fix is required. |
| P2 | [#292](https://github.com/ShareX/XerahS/issues/292) AUR build uses incompatible editor API | Parent repo builds with pinned editor `d7c027f`; external AUR recipe independently tracks editor heads. | Prevent dependency drift in a distributable repository-owned AUR recipe; document recovery and verify pinned API compilation. |
| P2 | [#284](https://github.com/ShareX/XerahS/issues/284) repository-owned AUR package | Partial: local-only recipe exists but requires `XERAHS_REPO_ROOT`; no `.SRCINFO` or dedicated CI. | Supply a normal remote-source recipe, generated metadata, packaging validation, and maintainer/publishing documentation. External AUR co-maintainer access is separate from repository work. |
| P2 | [#295](https://github.com/ShareX/XerahS/issues/295) confusing website download repositories | Stable upstream and fork preview channels are not explained consistently. | Document channel ownership and explicit download links in this fork; audit website source for the corresponding clarification. |
| Verified | [#297](https://github.com/ShareX/XerahS/issues/297) cancelled editor still saves/uploads | Already fixed by `c4b592b3`: null editor result stops after-capture tasks. | Re-run cancellation regression and verify finalization stops before save, copy, upload, history, and success notification. |

Each newly resolved issue receives a separate commit. Existing fixes are recorded with their original commits rather than duplicated. Platform-specific runtime checks and external packaging/website dependencies will be stated explicitly.

## Results in KovaForge/XerahS

| Issue | Implementation / evidence | Status and limits |
|---|---|---|
| #296 | `bb6ce580`; a hidden main window is no longer passed as an owner; background opens do not activate the viewer. Four headless visible/hidden × foreground/background cases pass. | Fixed in this fork. Linux Mint desktop runtime confirmation remains useful. |
| #294 | `508067e4`; persisted background monitoring defaults to false, is exposed in settings, and controls startup. A manual tool session stops when its window closes unless the user opted into background monitoring. | Fixed in this fork; 27 network monitor regressions pass, including legacy settings and shutdown policy. |
| #293 | `ca87f0a6`; sets Avalonia `EnableSessionManagement=false` and calls libc `unsetenv` as well as managed cleanup. | Both policy and real native/managed environment regressions pass. A full KDE/XFCE logout was not triggered on the user's desktop. |
| #298 | `f66868ca`, with library `KovaForge/ShareX.ImageEditor@fc2a4f3`; conversions use the actual channel and alpha formats, copy only pixel bytes per row, and check conversion failures. | Ten RGBA/BGRA, immutable/writable, padded-input and alpha round trips pass on Linux. Native macOS runtime still needs confirmation. |
| #290 | `e275afbb` and `d4420337`; drains both child output pipes, writes fragmented native MP4, propagates forced shutdown/timeouts, retains fatal errors, and rejects empty output or MP4 lacking complete metadata/media before success or upload. | Synthetic interrupted MP4 is recognized by ffprobe and decoded by FFmpeg. Earlier PipeWire FD, source selection and frame-padding fixes are preserved. The reporter's intermittent compositor/PipeWire stall itself has not been reproduced; fragmentation limits loss and failure paths no longer claim success. |
| #288 | Existing recovery `46c1ac1f`, strengthened by `f0a31be9`; keeps the recovery marker if Windows refuses to restore cursors, allowing another attempt at exit/startup. | Failure/exception/retry tests and Windows capture policy regressions pass on Linux. Attached logs contain updater DNS errors, but no usable stack for the original Windows crash. That crash is not independently diagnosed or claimed fixed. |
| #292 | `23fb0b87`; Arch builds explicitly check out and verify the parent's recursive submodule pins. The previously missing gradient API compiles against the pinned ImageEditor. | Local Git fixture tests reject missing and independently advanced dependencies. The external community AUR recipe must adopt these changes separately. |
| #284 | `90b119f8` and `b7b5157f`; public-source PKGBUILD, tracked generated .SRCINFO, declared dependencies, portable-only packaging, unprivileged Arch CI and maintainer instructions. | Metadata and shell checks pass. Final package build and payload checks succeeded; see evidence below. AUR ownership/transfer/publication is external and has not been performed. |
| #295 | `a886da68`; README links both repositories, explains release histories and build provenance, and the app settings explain which repository each update channel checks. The clone command targets KovaForge. | Clarified in this fork. The separately hosted upstream website is outside this repository and was not edited or deployed. The inspected website checkout currently fetches ShareX releases for both pages. |
| #297 | Already fixed by `c4b592b3`; `369ccc54` strengthens regression coverage through FinalizationStage for both Cancel and window close, with Save/Copy/Upload enabled. | Both cases stop the pipeline with Canceled status, no saved files and no upload URL. The earlier flattened-annotation upload tests also pass. |

## Verification

- Full desktop solution build: zero warnings, zero errors; final confirmation after all code changes also passed (30.93 seconds).
- Combined relevant regressions: 287 passed, zero failed/skipped, including actual GStreamer/FFmpeg integration. Final stop-race additions are checked separately below.
- Native GStreamer MP4 interruption test uses `videotestsrc`, waits for a complete fragment, kills the process, then validates the retained file using both ffprobe and FFmpeg decoding. The two missing plugins were unpacked under `/tmp` for testing; no system package was installed.
- Dependency guard fixtures, PKGBUILD shell syntax, YAML parsing and generated `.SRCINFO` consistency checks pass.
- Docker's socket is inaccessible to this session; the local package build uses installed host tools (`makepkg --nodeps`) without installing anything. The added CI job supplies the clean Arch environment; its result must be checked separately.
- Existing unrelated hourly-review edits are preserved and excluded from these commits.

The native MP4 choice follows [GStreamer's documented fragment-duration option](https://gstreamer.freedesktop.org/documentation/isomp4/GstBaseQTMux.html).
The Linux shutdown policy uses [Avalonia's documented session-management opt-out](https://docs.avaloniaui.net/docs/platform-specific-guides/linux).
Local partial-clone packaging uses [Git's documented GIT_NO_LAZY_FETCH override](https://git-scm.com/docs/git-upload-pack) only for an explicitly selected trusted local source.

These are source fixes on KovaForge's develop branch, not a new published release or an update of the running app. No upstream issues were closed or commented on.

### Final verification evidence

- Final stop-race suite: **32 passed**, zero failures/skips; covers reentrant error subscribers, already-completed bridge shutdown, interrupted native MP4 decoding and normal bridge finalization.
- First complete Arch package build succeeded with app, CLI, watch-folder daemon, twelve plugins, OmaSnap and its license notices: `xerahs-git-0.33.2.r5077.ga886da68-1-x86_64.pkg.tar.zst` (155 MiB). A final incremental build includes the subsequent race/packaging changes; its result is recorded below.

- Final complete package: `dist/aur/xerahs-git-0.33.2.r5080.gd4420337-1-x86_64.pkg.tar.zst`, built from `d4420337` with the repository-owned recipe. The packaged CLI's SQLite smoke check passed. Payload inspection verified app/CLI/daemon, twelve plugins, OmaSnap, MIT/OFL/ISC notices, desktop launcher and executable links. No host installation was performed.
- Final desktop solution build after all source changes: **0 warnings, 0 errors**.
- Code pushed to `KovaForge/XerahS/develop` at `d4420337`; dependency pushed to `KovaForge/ShareX.ImageEditor/develop` at `fc2a4f3`. Both pushes used the configured Declan wrapper.
- GitHub [Markdown Hygiene](https://github.com/KovaForge/XerahS/actions/runs/39177283175), [submodule enforcement](https://github.com/KovaForge/XerahS/actions/runs/39177283158), [Windows/Linux recording CI](https://github.com/KovaForge/XerahS/actions/runs/39177283156), and the corrected [clean Arch package CI](https://github.com/KovaForge/XerahS/actions/runs/39237601524) all passed. The initial Arch timeout and verified recovery are documented below.

## Remaining confirmation / external work

| Priority | Scope | Next action |
|---|---|---|
| P1 | Original Windows crash in #288 | Reproduce capture on Windows using the current fork; collect the faulting module/native dump or a crash stack. The supplied updater DNS errors cannot identify a capture crash. Cursor recovery is fixed independently. |
| P1 | Intermittent compositor stall in #290 | Repeat full-screen/window recording with and without audio on the reporter's compositor, including a long run. Check decoded output, stop latency and failure notification. Existing fixes and forced-interruption tests do not establish the cause of that intermittent stall. |
| P1 | Native macOS and Linux desktop checks | On macOS, annotate a screenshot with distinct red/blue pixels and inspect saved/uploaded output. In a disposable KDE/XFCE session, confirm logout with XerahS running. Do not log the user's active desktop out as a test. |
| P2 | Public AUR package (#284/#292) | Obtain co-maintainer authority, verify a clean graphical Arch install/start, then publish the reviewed recipe and metadata to the existing AUR package. The fork's commits do not alter the community listing. |
| P2 | Hosted upstream website (#295) | Apply the channel explanation in the website repository if maintainers still want both download sources presented, then deploy through its normal review process. This audit changes only KovaForge/XerahS and its dependency. |

### Clean CI follow-up

Windows x64 build/recording tests, Windows ARM64 compilation, and Linux
build/recording tests all passed in Native Recording run `39177283156`.
The first clean Arch job `39177283152` compiled the app and twelve plugins in about
three minutes, then stalled at the unbounded `dotnet build-server shutdown` call
until GitHub canceled the one-hour job. This is a build orchestration defect, not
an application compilation failure.

The follow-up bounds the shutdown client to 15 seconds (continuing with a diagnostic
if it fails), enforces serial plugin publishing in the Arch recipe, and exercises
successful, failed and stalled shutdown clients in CI. Shell/metadata checks and
the desktop solution build (0 warnings/errors) pass. The corrected clean Arch run verifies the complete recovery, as recorded below.

### Completed clean Arch verification

[Run 39237601524](https://github.com/KovaForge/XerahS/actions/runs/39237601524)
**passed** on `6237ebea`. Its log confirms the previously stalled shutdown client
again reached its timeout (exit 124), emitted the diagnostic after 15 seconds,
and packaging continued successfully. This exercises the actual CI failure path,
in addition to the mocked shutdown regressions.

The clean container built the app, CLI, daemon, twelve plugins and OmaSnap; the
CLI SQLite smoke check passed. It produced
`xerahs-git-0.33.2.r5084.g6237ebea-1-x86_64.pkg.tar.zst` and uploaded the
`xerahs-arch-package` artifact (162,296,475 bytes; artifact ID `12179504731`).
No CI time-limit increase was needed. Application fixes are in `d4420337`,
the ImageEditor dependency fix is `fc2a4f3`, and the additional packaging recovery
is `6237ebea`; all are pushed to their KovaForge repositories.
