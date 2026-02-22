# Cross-Platform Watch Folder Background Service Control

## Summary
Implement one global Start/Stop control in the **Watch Folders** tab to manage a dedicated headless watcher runtime (not the UI `XerahS` binary), with:
1. True Windows SCM service.
2. macOS and Linux daemons with both **User** and **System** scopes.
3. Auto install/update on Start.
4. Configurable startup mode.
5. Graceful stop with timeout.
6. Automatic daemon restart after watch-folder settings save.
7. In-app watchers disabled whenever daemon is running.

## Public APIs, Interfaces, and Types
1. Add `IWatchFolderDaemonService` in `src/XerahS.Platform.Abstractions/Services/IWatchFolderDaemonService.cs`.
2. Add `WatchFolderDaemonScope` enum with values `User` and `System`.
3. Add `WatchFolderDaemonStatus` and `WatchFolderDaemonResult` DTOs for status and operation results.
4. Add `UnsupportedWatchFolderDaemonService` default implementation.
5. Extend `PlatformServices` in `src/XerahS.Platform.Abstractions/PlatformServices.cs` with `WatchFolderDaemon` property and optional `watchFolderDaemonService` argument in `Initialize(...)`.
6. Add settings fields in `src/XerahS.Core/Models/ApplicationConfig.cs`:
   - `WatchFolderDaemonScope WatchFolderDaemonScope = WatchFolderDaemonScope.User;`
   - `bool WatchFolderDaemonStartAtStartup = true;`

## Headless Daemon Runtime
1. Add new project `src/XerahS.WatchFolder.Daemon/XerahS.WatchFolder.Daemon.csproj` and include it in `src/desktop/XerahS.sln`.
2. Implement `Program.cs` to run headless watcher loop using existing settings (`--settings-folder` argument supported).
3. Implement Windows-service-compatible hosting in the daemon binary so SCM can run it as a true service.
4. Implement POSIX signal/stop handling for graceful shutdown.
5. Reuse `SettingsManager` + `WatchFolderManager` for actual monitoring logic.
6. Add no-op UI/toast implementations in daemon project (or shared) so bootstrap remains headless-safe.

## Core Runtime Changes
1. Update `src/XerahS.Core/Managers/WatchFolderManager.cs` to expose public stop path and graceful wait for in-flight tasks.
2. Add `StopAsync(timeout)` behavior to support graceful daemon shutdown.
3. Keep `UpdateWatchers()` as reload entrypoint.

## Platform Implementations
1. Windows: add `src/XerahS.Platform.Windows/Services/WindowsWatchFolderDaemonService.cs`.
2. Windows behavior: system scope only, true SCM service, elevation required, startup toggle maps to `auto` vs `demand`.
3. Linux: add `src/XerahS.Platform.Linux/Services/LinuxWatchFolderDaemonService.cs`.
4. Linux behavior: support user scope (`systemd --user`) and system scope (`systemd`), scope-specific unit paths, elevation required for system scope.
5. macOS: add `src/XerahS.Platform.MacOS/Services/MacOSWatchFolderDaemonService.cs`.
6. macOS behavior: support LaunchAgent (user) and LaunchDaemon (system), elevation required for system scope.
7. Register each implementation in `WindowsPlatform.cs`, `LinuxPlatform.cs`, `MacOSPlatform.cs`, and `MobilePlatform.cs` (mobile uses unsupported implementation).
8. Use constant service identities:
   - Windows service name: `XerahSWatchFolder`.
   - Linux unit: `xerahs-watchfolder.service`.
   - macOS label: `com.sharexteam.xerahs.watchfolder`.

## UI and ViewModel Changes
1. Add global daemon control section to Watch Folders tab in `src/XerahS.UI/Views/ApplicationSettingsView.axaml`.
2. Place controls only in Watch Folders tab (not in `WatchFolderDialog`).
3. Add scope selector in UI for macOS/Linux and persist selection.
4. Hide scope selector on Windows and force system scope.
5. Add startup toggle (`Start at startup`) bound to `WatchFolderDaemonStartAtStartup`.
6. Add Start/Stop action (single toggle or two buttons) and status text.
7. Update `src/XerahS.UI/ViewModels/SettingsViewModel.cs` with daemon status properties and commands.
8. On settings save, if daemon is running, restart daemon automatically to apply changes.
9. Disable in-app watcher runtime when daemon is running; re-enable in-app runtime only when daemon is stopped.

## Startup and Packaging Integration
1. Update `src/XerahS.App/Program.cs` so app startup applies runtime mode once (daemon-owned vs in-process).
2. Update `src/XerahS.App/XerahS.App.csproj` to publish/copy daemon binary alongside app publish output for each RID.
3. Update `build/windows/XerahS-setup.iss` to include daemon executable in installer payload.
4. Keep Linux/mac packaging flows unchanged except they now include daemon binary from publish output.

## Test Cases and Scenarios
1. `ApplicationConfig` serialization/deserialization for daemon scope and startup fields.
2. `WatchFolderManager` graceful stop waits for in-flight tasks and respects timeout.
3. `SettingsViewModel` chooses daemon restart path when daemon is running.
4. `SettingsViewModel` suppresses in-process watcher activation while daemon is running.
5. Windows controller status parsing/install/start/stop behavior with elevation gating.
6. Linux controller unit/plumbing generation for user/system scopes.
7. macOS controller plist generation and start/stop/status for user/system scopes.
8. Manual E2E on each OS: Start installs+starts, Save restarts daemon, Stop performs graceful shutdown, status reflects reality.
9. Manual non-elevated system-scope attempt shows explicit elevation-required state and performs no mutation.

## Assumptions and Defaults
1. “Water Folder” is treated as “Watch Folder”.
2. Single global daemon/service monitors all enabled watch folders from existing settings.
3. Existing settings files remain the single source of truth for daemon.
4. Default daemon scope is `User` on macOS/Linux.
5. Windows always uses true system service (SCM).
6. System-scope operations require elevation and are blocked when not elevated.
7. Default graceful stop timeout is 30 seconds.
8. Default startup policy is enabled (`Start at startup = true`) but user-configurable.
