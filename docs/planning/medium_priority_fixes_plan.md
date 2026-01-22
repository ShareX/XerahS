# MEDIUM Priority Fixes - Implementation Plan

**Created**: 2026-01-18
**Status**: Ready for Implementation
**Total Issues**: 18
**Estimated Effort**: 4-6 hours

---

## Overview

This document outlines the implementation plan for 18 MEDIUM priority issues identified during the comprehensive code review. Issues are grouped into 4 logical batches based on subsystem and dependencies.

---

## Batch 1: Threading & Cancellation (5 issues)

**Estimated Time**: 1-1.5 hours
**Risk Level**: LOW-MEDIUM
**Dependencies**: None

### Issues

#### CORE-009: Redundant Task.Run Wrapper
- **File**: WorkerTask.cs:107
- **Fix**: Remove nested async lambda
- **Code Change**:
  ```csharp
  // Before:
  await Task.Run(async () => await DoWorkAsync(_cancellationTokenSource.Token));

  // After:
  await Task.Run(() => DoWorkAsync(_cancellationTokenSource.Token));
  ```
- **Validation**: Performance test 1000 tasks

#### CORE-010: Upload Cancellation Token
- **File**: WorkerTask.cs:524
- **Fix**: Pass task's cancellation token to upload processor
- **Code Change**:
  ```csharp
  await uploadProcessor.ProcessAsync(Info, _cancellationTokenSource.Token);
  ```
- **Validation**: Start upload, stop task immediately, verify cancellation

#### CORE-011: Recording Init Task Status Check
- **File**: ScreenRecordingManager.cs:447-452
- **Fix**: Check task faulted status before proceeding
- **Code Change**:
  ```csharp
  if (initTask != null)
  {
      await initTask;
      if (initTask.IsFaulted)
      {
          throw new InvalidOperationException("Recording init failed", initTask.Exception);
      }
  }
  ```
- **Validation**: Inject init failure, verify clear error message

#### WORKERTASK-001: Hard-Coded Window Activation Delay
- **File**: WorkerTask.cs:240, 259
- **Fix**: Make delay configurable via TaskSettings
- **Code Change**:
  ```csharp
  // Add to TaskSettings.AdvancedSettings:
  public int WindowActivationDelayMs { get; set; } = 250;

  // Use in WorkerTask:
  await Task.Delay(taskSettings.AdvancedSettings.WindowActivationDelayMs, token);
  ```
- **Validation**: Test with different delay values

#### CAPTURE-001: Capture Region Bounds Validation
- **File**: WindowsScreenCaptureService.cs:54-59
- **Fix**: Validate and clamp rect to screen bounds
- **Code Change**:
  ```csharp
  var screenBounds = _screenService.GetVirtualScreenBounds();
  x = Math.Max(x, screenBounds.X);
  y = Math.Max(y, screenBounds.Y);
  width = Math.Min(width, screenBounds.Right - x);
  height = Math.Min(height, screenBounds.Bottom - y);

  if (width <= 0 || height <= 0)
  {
      DebugHelper.WriteLine("Capture region outside screen bounds");
      return null;
  }
  ```
- **Validation**: Test capture outside screen bounds

---

## Batch 2: Error Handling & Logging (4 issues)

**Estimated Time**: 1-1.5 hours
**Risk Level**: LOW
**Dependencies**: None

### Issues

#### LOGGER-001: File Write Failure Tracking
- **File**: Logger.cs:122-167
- **Fix**: Track consecutive failures, disable logging after threshold
- **Code Change**:
  ```csharp
  private int _consecutiveFileWriteFailures = 0;
  private const int MaxConsecutiveFailures = 5;

  try
  {
      File.AppendAllText(currentLogPath, message, Encoding.UTF8);
      _consecutiveFileWriteFailures = 0;
  }
  catch (Exception e)
  {
      _consecutiveFileWriteFailures++;
      if (_consecutiveFileWriteFailures >= MaxConsecutiveFailures)
      {
          FileWrite = false;
          Debug.WriteLine($"Disabled file logging after {MaxConsecutiveFailures} failures");
      }
  }
  ```
- **Validation**: Test with read-only log folder

#### BOOTSTRAP-002: DateTime.Now Called Twice
- **File**: ShareXBootstrap.cs:109
- **Fix**: Capture once to avoid midnight boundary issue
- **Code Change**:
  ```csharp
  var now = DateTime.Now;
  var logsFolder = Path.Combine(baseFolder, "Logs", now.ToString("yyyy-MM"));
  logPath = Path.Combine(logsFolder, $"ShareX-{now:yyyy-MM-dd}.log");
  ```
- **Validation**: Test at month boundary

#### WORKERTASK-002: Error Message Truncation
- **File**: WorkerTask.cs:123-126
- **Fix**: Truncate at word boundary
- **Code Change**:
  ```csharp
  const int MaxErrorMessageLength = 150;
  if (errorMessage.Length > MaxErrorMessageLength)
  {
      int truncateAt = errorMessage.LastIndexOf(' ', MaxErrorMessageLength - 3);
      if (truncateAt < MaxErrorMessageLength / 2)
          truncateAt = MaxErrorMessageLength - 3;
      errorMessage = errorMessage.Substring(0, truncateAt) + "...";
  }
  ```
- **Validation**: Test with long error message containing emoji

#### HISTORY-002: DBNull Handling in Load
- **File**: HistoryManagerSQLite.cs:101-125
- **Fix**: Check for DBNull before ToString
- **Code Change**:
  ```csharp
  string? tagsJson = null;
  if (reader["Tags"] != DBNull.Value)
  {
      tagsJson = reader["Tags"]?.ToString();
  }

  Tags = string.IsNullOrEmpty(tagsJson)
      ? new Dictionary<string, string?>()
      : JsonConvert.DeserializeObject<Dictionary<string, string?>>(tagsJson) ?? new Dictionary<string, string?>()
  ```
- **Validation**: Test with NULL tags in database

---

## Batch 3: Code Quality & Refactoring (5 issues)

**Estimated Time**: 1.5-2 hours
**Risk Level**: LOW
**Dependencies**: None

### Issues

#### SETTINGS-003: Duplicate Machine-Specific Config Logic
- **File**: SettingsManager.cs:383-470
- **Fix**: Extract common logic to shared method
- **Code Change**:
  ```csharp
  private static string GetMachineSpecificConfigFileName(
      string destinationFolder,
      string configPrefix,
      string configExtension,
      bool useMachineSpecific)
  {
      // ... extracted common logic ...
  }

  private static string GetUploadersConfigFileName(string destinationFolder)
  {
      return GetMachineSpecificConfigFileName(
          destinationFolder,
          UploadersConfigFileNamePrefix,
          UploadersConfigFileNameExtension,
          Settings?.UseMachineSpecificUploadersConfig ?? false);
  }
  ```
- **Validation**: Test both config types with machine-specific naming

#### PLATFORM-004: Hard-Coded Window Class Filter
- **File**: WindowsWindowService.cs:118-123
- **Fix**: Move to static readonly HashSet with documentation
- **Code Change**:
  ```csharp
  private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.OrdinalIgnoreCase)
  {
      "Progman",                      // Program Manager (desktop)
      "Button",                       // System button windows
      "Shell_TrayWnd",                // Taskbar
      "Shell_SecondaryTrayWnd",       // Secondary taskbar (multi-monitor)
      "Windows.UI.Core.CoreWindow",   // UWP system windows
  };

  if (IgnoredWindowClasses.Contains(className))
      return true;
  ```
- **Validation**: Enumerate windows, verify filter works

#### CORE-012: Magic Numbers in Video Dimension Adjustment
- **File**: WorkerTask.cs:352-369
- **Fix**: Extract constants with documentation
- **Code Change**:
  ```csharp
  // H.264/H.265 encoders require even dimensions
  private const int VIDEO_DIMENSION_ALIGNMENT = 2;
  private const int MIN_VIDEO_WIDTH = 2;
  private const int MIN_VIDEO_HEIGHT = 2;

  int adjustedWidth = selection.Width - (selection.Width % VIDEO_DIMENSION_ALIGNMENT);
  int adjustedHeight = selection.Height - (selection.Height % VIDEO_DIMENSION_ALIGNMENT);

  if (adjustedWidth < MIN_VIDEO_WIDTH || adjustedHeight < MIN_VIDEO_HEIGHT)
  {
      TroubleshootingHelper.Log(..., $"Region too small: {adjustedWidth}x{adjustedHeight}");
      return;
  }
  ```
- **Validation**: Test with odd dimensions

#### TASKHELPERS-001: WindowTitle Null Safety
- **File**: TaskHelpers.cs:231-239, 287-298
- **Fix**: Use IsNullOrWhiteSpace consistently
- **Code Change**:
  ```csharp
  nameParser.WindowText = metadata?.WindowTitle ?? string.Empty;

  if (!string.IsNullOrWhiteSpace(settings.SaveImageSubFolderPatternWindow) &&
      !string.IsNullOrWhiteSpace(nameParser.WindowText))
  {
      subFolderPattern = settings.SaveImageSubFolderPatternWindow;
  }
  ```
- **Validation**: Test window capture with pattern

#### PLATFORM-003: SKBitmap Decode Stream Lifetime
- **File**: WindowsScreenCaptureService.cs:231-238
- **Fix**: Add verification comment and null check
- **Code Change**:
  ```csharp
  private SKBitmap? ToSKBitmap(Bitmap bitmap)
  {
      using (var stream = new MemoryStream())
      {
          bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
          stream.Seek(0, SeekOrigin.Begin);

          // SKBitmap.Decode creates a copy of pixel data, safe to dispose stream
          var skBitmap = SKBitmap.Decode(stream);

          if (skBitmap == null)
          {
              DebugHelper.WriteLine("Failed to decode bitmap to SKBitmap");
          }

          return skBitmap;
      }
  }
  ```
- **Validation**: Test large bitmap, access pixels after conversion

---

## Batch 4: UX & Settings (4 issues)

**Estimated Time**: 1.5-2 hours
**Risk Level**: MEDIUM (user-facing changes)
**Dependencies**: May require UI updates

### Issues

#### TASKHELPERS-002: FileExistAction.Ask Not Implemented
- **File**: TaskHelpers.cs:361-393
- **Fix**: Return empty string to signal UI decision needed
- **Code Change**:
  ```csharp
  case FileExistAction.Ask:
      // Ask requires UI interaction - return empty to signal caller
      DebugHelper.WriteLine($"File exists, action is Ask: {filePath}");
      return string.Empty; // Signal caller to show UI

  default:
      DebugHelper.WriteLine($"Unknown FileExistAction, using unique name");
      return FileHelpers.GetUniqueFilePath(filePath);
  ```
- **Validation**: Set Ask, take duplicate screenshot, verify behavior
- **Notes**: May need UI implementation or setting removal

#### SETTINGS-002: ResetSettings Backup
- **File**: SettingsManager.cs:475-482
- **Fix**: Create backup before deleting
- **Code Change**:
  ```csharp
  public static bool ResetSettings()
  {
      try
      {
          var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
          var backupFolder = Path.Combine(BackupFolder, $"Reset_{timestamp}");
          Directory.CreateDirectory(backupFolder);

          if (File.Exists(ApplicationConfigFilePath))
          {
              File.Copy(ApplicationConfigFilePath,
                  Path.Combine(backupFolder, "ApplicationConfig.json"), true);
              File.Delete(ApplicationConfigFilePath);
          }

          // Similar for other configs...

          Settings = new ApplicationConfig();
          UploadersConfig = new UploadersConfig();
          WorkflowsConfig = new WorkflowsConfig();

          DebugHelper.WriteLine($"Settings reset. Backup: {backupFolder}");
          return true;
      }
      catch (Exception ex)
      {
          DebugHelper.WriteException(ex, "Failed to reset settings");
          return false;
      }
  }
  ```
- **Validation**: Call ResetSettings, verify backup created
- **Notes**: Consider adding user confirmation UI

#### Remaining 2 issues in review doc
- To be detailed after reading full issue list

---

## Implementation Guidelines

### General Principles

1. **One Issue Per Commit**: Each fix should be a separate commit with reference to issue ID
2. **Test Before Commit**: Verify build succeeds with 0 errors/warnings
3. **Add Tests**: Where possible, add unit tests for fixes
4. **Update Docs**: Update XML docs if API behavior changes
5. **Breaking Changes**: None expected, but flag if any arise

### Build Verification

After each batch:
```bash
dotnet build XerahS.sln -c Debug
dotnet build XerahS.sln -c Release
```

Expected: 0 errors, 0 warnings

### Commit Message Format

```
XerahS: Fix [ISSUE-ID] - [Short Description]

[Detailed description of the fix]

Issue: [ISSUE-ID]
Severity: MEDIUM
Category: [Category]

Files Modified:
- src/[Project]/[File].cs

Validation:
- [Test scenario 1]
- [Test scenario 2]

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
```

### Testing Checklist

For each batch:
- [ ] Build succeeds (Debug + Release)
- [ ] No new warnings introduced
- [ ] Specific validation tests pass
- [ ] No regressions in existing functionality
- [ ] Git hooks pass (license headers)

---

## Risk Mitigation

### High-Risk Changes

- **TASKHELPERS-002**: May need UI implementation
- **SETTINGS-002**: Destructive operation, needs user confirmation

### Mitigation Strategies

1. **Feature Flags**: Consider adding feature flags for UX changes
2. **Gradual Rollout**: Test on development branch before merging
3. **User Communication**: Document breaking changes in release notes
4. **Rollback Plan**: Each batch can be reverted independently

---

## Progress Tracking

### Batch 1: Threading & Cancellation
- [ ] CORE-009: Redundant Task.Run
- [ ] CORE-010: Upload cancellation
- [ ] CORE-011: Recording init check
- [ ] WORKERTASK-001: Activation delay
- [ ] CAPTURE-001: Bounds validation

### Batch 2: Error Handling & Logging
- [ ] LOGGER-001: File write failure tracking
- [ ] BOOTSTRAP-002: DateTime.Now call
- [ ] WORKERTASK-002: Error truncation
- [ ] HISTORY-002: DBNull handling

### Batch 3: Code Quality & Refactoring
- [ ] SETTINGS-003: Duplicate config logic
- [ ] PLATFORM-004: Window class filter
- [ ] CORE-012: Magic numbers
- [ ] TASKHELPERS-001: WindowTitle null safety
- [ ] PLATFORM-003: SKBitmap decode

### Batch 4: UX & Settings
- [ ] TASKHELPERS-002: FileExistAction.Ask
- [ ] SETTINGS-002: ResetSettings backup
- [ ] [Additional issues...]

---

## Success Criteria

- [ ] All 18 MEDIUM issues resolved
- [ ] Build health maintained (0 errors, 0 warnings)
- [ ] All validation tests pass
- [ ] Documentation updated
- [ ] Git hooks configured and tested
- [ ] Changes committed and pushed
- [ ] Final report generated

---

**Next Steps**:
1. Review and approve this plan
2. Begin Batch 1 implementation
3. Test and commit each batch
4. Generate progress report after each batch
5. Final validation and summary

---

**Last Updated**: 2026-01-18
**Status**: Ready for implementation
**Estimated Completion**: 1-2 days (depending on testing depth)
