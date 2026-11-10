# Implicit state machines audit

Scope: places where lifecycle state is spread across booleans, several enums or event ordering instead of one explicit, validated state. Found by searching `src/` for state/status enums and `_isX = true/false` flag clusters (about 218 flag assignments in 58 files). Findings below were checked by reading the code at `ddfb6083`; items marked *unverified* were only spotted by name.

Existing precedent: `SelectionStateMachine` (`XerahS.RegionCapture/Services`) is the one explicit machine. Use its shape (one state enum, one transition entry point, state exposed read-only) as the template.

## Findings, ranked by risk

| # | Machine | Where | Why it is implicit |
|---|---------|-------|--------------------|
| 1 | Screen recording lifecycle | `ScreenRecordingManager` (`_isPaused`, `_isFinalized`, `_currentRecording`), `RecordingViewModel` (`Status`, `IsRecording`, `IsPaused`, `CanStart/Stop/PauseResume/Abort`) | The manager infers state from two bools plus a null check and emits `RecordingStatus` events by hand (about 14 flag writes). The view model then copies each status into 7 mirrored properties. Illegal combinations are representable and nothing rejects an invalid transition. |
| 2 | Three overlapping recording enums | `RecordingStatus` (6 values), `ScreenRecordingStatus` (Waiting/Working/Recording/Paused/Stopped/Aborted), `ScreenRecordState` (Waiting/BeforeStart/…/Encoding); the last two are declared twice (Core and RegionCapture) | Same concept modelled several times with no mapping contract. |
| 3 | Upload task lifecycle | `TaskStatus` in `Core/Enums.cs` set in `WorkerTask` (13 sites), `CaptureStage` (14), `FinalizationStage` | The enum exists but transitions are plain assignments scattered across the pipeline stages; `Stopping/Stopped/Canceled/Failed` ordering is by convention. |
| 4 | Upload queue | `UploadQueueService._isProcessing`; `UploadQueueItemStatus` in `UploadContentViewModel` | A single "is the pump running" flag guarded by locks, separate from per-item status. |
| 5 | Watch-folder daemon / manager | `WatchFolderDaemonState`, platform daemon services, `WatchFolderManager._isDisposed` | State enum plus disposal flag plus process liveness; recovery logic (`WatchedFileUploadRecovery`) re-derives state from files and history. |
| 6 | Hotkey backend | `HotkeyBackendState`, `HotkeyStatus` (declared in both Platform.Abstractions and Common) | Duplicate enum definitions; Linux service keeps extra flags. |
| 7 | Plugin config / OAuth flows | `PluginVerificationStatus`, `OAuthLoginStatus`, Nextcloud/Immich/FTP config view models (about 12 flag writes each) | Per-view-model busy/connected/verified flags. *Unverified in detail.* |
| 8 | Update / startup / network | `UpdateStatus`, `StartupState`, `InternetConnectionMonitor` | Small, mostly well-formed enums. Low priority. |
| 9 | Tool view models | Image tools, OCR, QR, thumbnailers, `IndexFolderViewModel` (3 to 7 busy flags each) | Same busy/idle pattern copied per view model. Candidate for a shared base, not a state machine each. |

## Recommended decision

Do not add a state-machine library and do not refactor everything. The best trade-off for users, developers and agents is a small, incremental path that changes no behaviour at each step:

1. **Recording first (#1, #2).** It has the most flags, the most user-visible failure modes (stuck "Finalizing", pause/resume races) and the clearest enum to build on. Introduce one internal `RecordingStateMachine` in Core that owns `RecordingStatus`, exposes `TryTransition(from → to)` with a documented transition table, and is the only thing that raises `StatusChanged`. Replace `_isPaused`/`_isFinalized` with reads of it. In `RecordingViewModel`, derive `IsRecording`, `IsPaused` and the `Can*` flags from `Status` in one place instead of mirroring them.
2. **Make illegal transitions loud but non-fatal.** An invalid transition logs through `DebugHelper` and is ignored. That keeps current behaviour on every path that works today.
3. **Unit-test the transition table** (valid and invalid pairs). Start from the `SelectionStateMachine` tests if present.
4. **Then TaskStatus (#3)** the same way, and de-duplicate the mirrored enums (#2, #6) by keeping one definition.
5. Leave #7 to #9 until they cause a bug.

## Why this helps each audience

- **UX:** fewer stuck or contradictory UI states (button enabled while finalizing, tray icon out of sync), because there is one source of truth and invalid transitions cannot half-apply.
- **DX:** a transition table is the documentation. Adding a state means editing one table and its tests, not hunting flag writes.
- **AX:** an agent can read one file to learn the legal states and transitions, and tests give it a safety net for changes. Today it must infer them from about 14 scattered assignments.

## Status

**Step 1, recording (done, observe-only).** `RecordingStateMachine` (transition table, logs unexpected transitions, never blocks) and `RecordingUiState.From` (status text and button availability) live in `Core/Managers/RecordingStateMachine.cs`. `ScreenRecordingManager` routes every `StatusChanged` through the machine, and `RecordingViewModel` derives its flags from `RecordingUiState`. `_isPaused`/`_isFinalized` are intentionally still in the manager; they also drive segment and abort logic, so replacing them is a separate step once the table has proven itself in logs. Tests: `RecordingStateMachineTests`.

**Step 4a, task lifecycle #3 (done).** `TaskStatusMachine` in `Core/Tasks/TaskStatusMachine.cs` holds the `TaskStatus` transition table and the three groupings every caller needs (`IsWorking`, `IsFinished`, `IsUnsuccessfulEnd`). `WorkerTask.Status` reads and writes through it, so there is one write path. Tests: `TaskStatusMachineTests`. Two behaviours changed on purpose, both bugs of the implicit version:

- A capture cancelled during its start delay was set to `Stopped`, then reset to `Working` when the pipeline's stale status copy was synced back, and so ended as `Completed` (the CLI reported success). `TaskStatusMachine.ResolveAfterPipeline` now keeps the task's own status unless a stage decided an outcome.
- A finished task never goes back to an unfinished status. A `Stop()` that loses the race with completion used to be able to leave a finished task on `Stopping` (and `IsBusy`) for good; that write is now ignored and logged.

Every other unexpected transition is logged and still applied, as in step 1. Still open in #3: the pipeline keeps its own status copy (`PipelineContext.Status`), and `PipelineStageResult.Stop` means both "finished here" and "cancelled"; a stage that returns `Stop` without setting a status ends as `Completed`.

**Step 4b, duplicate recording enums #2 (done).** `ScreenRecordingStatus` and `ScreenRecordState` had no references anywhere; all four declarations (Core and RegionCapture) are removed. `RecordingStatus` is the only recording lifecycle enum.

**Not started.** #6: the two `HotkeyStatus` enums have different members (`Common` has 3, `Platform.Abstractions` has 5) and about 80 uses, so merging them needs a mapping decision first. #4, #5, #7 to #9 as ranked above.

## Found later (not in the first audit)

| # | Machine | Where | Why it is implicit |
|---|---------|-------|--------------------|
| 10 | Toast lifetime | `ToastViewModel` (`_isDurationEnd`, `_isMouseInside`, `_isHovered`, `_isMenuOpen`, `_isFileDragActive`, plus the fade, leave-grace and deferred-close timers) | Whether a toast may close is the product of five flags and three timers. |
| 11 | Editor pointer interaction | `ShareX.ImageEditor`: `EditorInputController` (24 flag writes), `EditorSelectionController`, `EditorCore` | Drawing, dragging, resizing and panning are tracked by separate flags. Lives in the ShareX.ImageEditor repository, so it is changed there and ported. |
| 12 | Task disposal | `WorkerTask` (`_disposeRequested`, `_disposed`, `_hasFinished`) | A second small lifecycle beside `TaskStatus`; guarded by one lock and covered by `WorkerTaskImageLifetimeTests`. Leave as is. |
