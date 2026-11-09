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

Step 1 is implemented in observe-only form: `RecordingStateMachine` (transition table, logs unexpected transitions, never blocks) and `RecordingUiState.From` (status text and button availability) live in `Core/Managers/RecordingStateMachine.cs`. `ScreenRecordingManager` routes every `StatusChanged` through the machine, and `RecordingViewModel` derives its flags from `RecordingUiState`. `_isPaused`/`_isFinalized` are intentionally still in the manager; they also drive segment and abort logic, so replacing them is a separate step once the table has proven itself in logs. Tests: `RecordingStateMachineTests`.

Steps 2 onward (TaskStatus, duplicate enums) are not started.
