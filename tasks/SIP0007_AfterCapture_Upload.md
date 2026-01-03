# CX04: AfterCaptureJobs - Upload Processor Backend

## Priority
**HIGH** - Enables automatic upload workflow

## Assignee
**Codex** (Surface Laptop 5, VS Code)

## Branch
`feature/after-capture-upload`

## Instructions
**CRITICAL**: Create the `feature/after-capture-upload` branch first before starting work.

```bash
git checkout master
git pull origin master
git checkout -b feature/after-capture-upload
```

## Objective
Implement `UploadImageToHost` in `CaptureJobProcessor` to enable automatic image uploads after capture. This completes the ShareX workflow: **Capture → AfterCapture → Upload → AfterUpload**.

## Background
Currently, only 3/21 AfterCaptureTasks work:
- ✅ SaveImageToFile
- ✅ CopyImageToClipboard
- ✅ AnnotateImage
- ❌ **UploadImageToHost** ← Your task

`UploadImageToHost` is defined in the `AfterCaptureTasks` enum (line 148 in Enums.cs) but not implemented in `CaptureJobProcessor.cs`.

## Scope

### 1. Implement Upload Handler in CaptureJobProcessor

**File**: `src/ShareX.Avalonia.Core/Tasks/Processors/CaptureJobProcessor.cs`

Add after line 46 (after AnnotateImage handling):

```csharp
if (settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost))
{
    await UploadImageAsync(info);
}
```

### 2. Add UploadImageAsync Method

In the same file, add the upload logic:

```csharp
private async Task UploadImageAsync(TaskInfo info)
{
    // Ensure image is saved to file first (uploaders need file path)
    if (string.IsNullOrEmpty(info.FilePath) && info.Metadata?.Image != null)
    {
        // Save temp file if not already saved
        info.FilePath = TaskHelpers.SaveImageAsFile(info.Metadata.Image, info.TaskSettings);
    }
    
    if (string.IsNullOrEmpty(info.FilePath))
    {
        DebugHelper.WriteLine("Upload failed: No file to upload");
        return;
    }

    DebugHelper.WriteLine($"Uploading image: {info.FilePath}");
    
    // TODO: Get uploader from UploadManager
    // var uploader = UploadManager.GetImageUploader(settings.ImageDestination);
    // var result = await uploader.UploadFileAsync(info.FilePath);
    // info.UploadResult = result;
    // info.Metadata.UploadURL = result.URL;
    
    // For now, just log that upload would happen here
    DebugHelper.WriteLine($"Upload logic placeholder - would upload to: {info.TaskSettings.ImageDestination}");
    
    await Task.CompletedTask;
}
```

### 3. Update TaskMetadata (Optional)

**File**: `src/ShareX.Avalonia.Core/Models/TaskMetadata.cs`

Add property to store upload URL:

```csharp
public string? UploadURL { get; set; }
```

## Guidelines
- **Use existing UploadManager API** (check `ShareX.Avalonia.Uploaders` for reference)
- **Ensure image is saved before upload** (uploaders need file paths)
- **Add debug logging** for upload start/complete/failure
- **Handle upload errors gracefully** - don't crash if upload fails
- **XML doc comments** for public methods

## Integration Notes

This task coordinates with **CP03** (Copilot's UI task). The UI will toggle `TaskSettings.AfterCaptureJob` flags, which your code reads.

**Don't worry about**:
- UI for toggling upload checkbox (Copilot handles this in CP03)
- AfterUpload tasks (future work)

## Deliverables
- ✅ `CaptureJobProcessor.cs` updated with UploadImageToHost handler
- ✅ Build succeeds on `feature/after-capture-upload`
- ✅ Debug logs show upload attempt when flag is set
- ✅ Commit and push changes

## Testing

### Manual Test
1. **Set upload flag programmatically** in default task settings:
   ```csharp
   // In ApplicationConfig.cs or hotkey settings
   AfterCaptureJob = AfterCaptureTasks.SaveImageToFile | AfterCaptureTasks.UploadImageToHost
   ```

2. **Trigger hotkey** (Ctrl+PrintScreen)

3. **Check Debug view** - should see:
   ```
   Image saved: C:\...\screenshot.png
   Uploading image: C:\...\screenshot.png
   Upload logic placeholder - would upload to: Imgur
   ```

4. **Verify**: No crashes, upload code path executes

## Estimated Effort
**Medium** - 2-3 hours
