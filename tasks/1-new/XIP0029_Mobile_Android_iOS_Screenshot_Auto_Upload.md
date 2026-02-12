# AI Agent Instruction Set  
## Add Mobile Android and iOS Projects to XerahS with Screenshot Auto Upload

You are an experienced .NET architect working inside the ShareX Team ecosystem.  
Your objective is to add full mobile support (Android and iOS) to XerahS in a way that:

- Reuses existing infrastructure
- Avoids duplicate logic
- Integrates cleanly with TaskManager and Uploaders
- Works end to end with AmazonS3 and CustomUploader
- Produces a URL after upload
- Follows existing architectural patterns

You must study the current repository before writing code. Do not introduce parallel systems.

---

# 1. Study Existing Architecture First

Before implementing anything, examine:

## 1.1 WatchFolderManager
Located in:
src/XerahS.Core/Managers/WatchFolderManager.cs

Understand:
- How FileSystemWatcher is used
- How workflow is retrieved via SettingsManager
- How TaskSettings are cloned
- How TaskManager.Instance.StartFileTask is invoked

Key pattern:
- Detect file
- Wait for file ready
- Clone workflow TaskSettings
- Set Job = WorkflowType.FileUpload
- Start file task via TaskManager

Mobile implementation must replicate this pattern, not bypass it.

---

## 1.2 Upload Infrastructure

Study:

- WorkerTaskUpload.cs
- UploadJobProcessor
- TaskManager
- UploaderProviderBase
- AmazonS3Provider
- CustomUploaderProvider
- Secrets handling

Important:
Mobile must NOT upload files directly.
Mobile must trigger TaskManager the same way desktop does.

The upload pipeline must remain:

DetectedFile  
→ TaskSettings  
→ TaskManager  
→ WorkerTask  
→ UploadProcessor  
→ Provider  
→ UploadResult.URL

---

# 2. Create New Mobile Project

Create a new project:

src/XerahS.Mobile/

Use .NET MAUI for multi-targeting Android and iOS.

Create:

XerahS.Mobile.csproj

Multi-target:
- net8.0-android
- net8.0-ios

Reference:

- XerahS.Core
- XerahS.Common
- XerahS.Uploaders
- XerahS.Uploaders.PluginSystem
- Any shared abstraction assemblies

DO NOT copy logic from desktop.
Reference shared projects.

---

# 3. Define Screenshot Monitoring Abstraction

Create interface in XerahS.Core or XerahS.Platform.Abstractions:

```csharp
public interface IScreenshotMonitorService
{
    void Start();
    void Stop();
    event EventHandler<string> ScreenshotDetected;
}
```

Mobile project must implement this interface per platform.

Do not embed platform logic into Core.

---

# 4. Android Implementation

Create:

Platforms/Android/AndroidScreenshotMonitorService.cs

Requirements:

- Monitor screenshots folder:
  /DCIM/Screenshots
- Use ContentObserver or FileObserver
- Handle runtime permission:
  READ_MEDIA_IMAGES (Android 13+)
  READ_EXTERNAL_STORAGE (older versions)

When file detected:
- Ensure file fully written
- Raise ScreenshotDetected with absolute path

DO NOT upload here.
Only detect and emit event.

---

# 5. iOS Implementation

Create:

Platforms/iOS/IosScreenshotMonitorService.cs

Use:

UIApplication.UserDidTakeScreenshotNotification

After notification:
- Query Photos library for most recent image
- Require user permission to access photos

Raise ScreenshotDetected with local file path copy.

Important:
iOS does not allow arbitrary filesystem watching.
Use notification + PHPhotoLibrary.

---

# 6. Mobile Bootstrap Integration

In XerahS.Mobile startup:

- Resolve IScreenshotMonitorService
- Subscribe to ScreenshotDetected

When event fires:

Call:

HandleScreenshot(string path)

---

# 7. Implement HandleScreenshot Logic

This must mirror WatchFolderManager behavior.

Pseudo flow:

1. Retrieve selected workflow from SettingsManager
2. Clone TaskSettings using existing JSON clone mechanism
3. Set:
   clonedSettings.Job = WorkflowType.FileUpload
4. Call:
   await TaskManager.Instance.StartFileTask(clonedSettings, path)

Do NOT manually call upload providers.

Reuse full TaskManager pipeline.

---

# 8. Mobile Settings UI

Create simple MAUI page:

MobileSettingsPage

Allow user to configure:

- Enable Auto Upload toggle
- Select Workflow
- Select Destination instance
- Configure AmazonS3 or CustomUploader instance
- View last uploaded URL

Reuse:

- UploaderProviderBase
- ConfigModelType
- ProviderId

Do not reimplement configuration logic.

---

# 9. Ensure No Duplicate Upload Logic

STRICT RULES:

- No HTTP upload code in mobile project
- No direct S3 client in mobile project
- No reimplementation of CustomUploader
- No duplicate file naming logic
- No duplicate URL generation logic

All must flow through:

TaskManager → WorkerTask → UploadProcessor → Provider

---

# 10. File Naming and Storage

Use:

TaskHelpers.GetScreenshotsFolder
TaskHelpers.GetFileName
TaskHelpers.HandleExistsFile

Do not create mobile-specific naming systems.

---

# 11. Permissions Handling

Android:
- Request runtime permission
- Gracefully handle denial

iOS:
- Request Photo Library permission
- Handle limited access mode

---

# 12. Background Execution

Android:
- Use foreground service if necessary
- Prevent OS killing monitor

iOS:
- Background execution limited
- Accept that detection only works while app running

Document limitations clearly.

---

# 13. Testing Plan

Test scenarios:

- Take screenshot → auto upload
- Multiple rapid screenshots
- Large file upload
- Network interruption
- Provider failure
- Permission revoked

Verify:

UploadResult.URL populated correctly

---

# 14. Architectural Integrity Checklist

Before committing:

- No duplicated upload code
- No duplicated watcher logic
- Core remains platform neutral
- Platform specific code isolated in MAUI Platforms folder
- All uploads go through TaskManager
- No provider logic rewritten

---

# 15. Deliverables

You must deliver:

1. XerahS.Mobile project
2. Android screenshot monitor implementation
3. iOS screenshot monitor implementation
4. Integration into TaskManager
5. Settings page
6. Working AmazonS3 upload from mobile
7. Working CustomUploader upload from mobile
8. Clean build for Android and iOS
9. Documentation in docs/mobile_support.md

---

# End Goal

When a user takes a screenshot on mobile:

Screenshot taken  
→ Mobile detects file  
→ TaskManager invoked  
→ Selected uploader runs  
→ UploadResult returned  
→ URL shown to user  

All without duplicating infrastructure.

This must behave like desktop WatchFolderManager, but adapted to mobile OS constraints.
