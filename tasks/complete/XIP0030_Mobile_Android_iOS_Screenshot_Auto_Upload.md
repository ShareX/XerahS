# XIP0030: Mobile Android and iOS Support via Avalonia Mobile

**Status**: Complete
**Created**: 2026-02-12
**Completed**: 2026-02-13
**Area**: Mobile / Cross-Platform
**Goal**: Implement "Share to XerahS" on iOS and Android using Avalonia, then upload using existing XerahS pipeline.

## Implementation Notes

The implementation improved on XIP0030's original design:

- **No ShareImportService**: The ViewModel calls `TaskManager.StartFileTask()` directly, reusing `WatchFolderManager.CloneTaskSettings()` (same pattern as desktop watch folders). This avoids an unnecessary abstraction layer.
- **Separate mobile head projects**: Instead of polluting `XerahS.App` with mobile code, created dedicated projects (`XerahS.Mobile.Android`, `XerahS.Mobile.iOS`) with a shared `XerahS.Mobile.UI` layer.
- **XerahS.Platform.Mobile**: Comprehensive stub implementations for all 11 `PlatformServices` interfaces, ensuring the Core pipeline works on mobile without crashes.
- **No new settings model for MVP**: Uses `DefaultTaskSettings` directly. Settings customization can be added later.
- **XerahS.Core multi-targeting**: Added `net10.0` alongside `net10.0-windows` so mobile projects can reference it.

### Projects Created
| Project | TFM | Files | Purpose |
|---------|-----|-------|---------|
| `XerahS.Platform.Mobile` | `net10.0` | 13 | Mobile platform service stubs |
| `XerahS.Mobile.UI` | `net10.0` | 6 | Shared Avalonia mobile app + views |
| `XerahS.Mobile.Android` | `net10.0-android` | 5 | Android entry point + share intents |
| `XerahS.Mobile.iOS` | `net10.0-ios` | 5 | iOS entry point + URL scheme handler |
| `XerahS.Mobile.iOS.ShareExtension` | `net10.0-ios` | 4 | iOS Share Extension with App Groups |

### Existing Files Modified
- `IPlatformInfo.cs` - Added `Android`, `iOS` to `PlatformType` enum
- `WatchFolderManager.cs` - Made `CloneTaskSettings` public
- `XerahS.Core.csproj` - Multi-target `net10.0` + `net10.0-windows`

---

## Overview

Implement a **"Share to XerahS"** feature that allows users to share screenshots or any files from other apps to XerahS on mobile. XerahS then uploads using the existing `TaskManager` and uploader providers.

**Key Principles:**
- No automatic folder monitoring (battery/permission friendly)
- User explicitly shares content (privacy first)
- Zero duplicate upload logic
- Reuse existing pipeline end-to-end

---

## Prerequisites

- Android SDK (API 21+)
- Xcode (for iOS)
- .NET 10 SDK with mobile workloads:
  ```bash
  dotnet workload install android ios
  ```

---

## Phase 1: Study Existing Pipeline

Study these areas and map the full flow from file path to `UploadResult` URL:

### 1.1 WatchFolderManager Pattern
Located in `XerahS.Core/Managers/WatchFolderManager.cs`

Understand:
- File readiness detection (wait for file to be fully written)
- `TaskSettings` cloning via JSON serialization
- Setting `Job = WorkflowType.FileUpload`
- Calling `TaskManager.Instance.StartFileTask()`

### 1.2 Upload Pipeline
- **TaskManager** - Entry point for all tasks
- **WorkerTask** - Executes the upload job
- **UploadJobProcessor** - Processes upload logic
- **UploaderProviderBase** - Base for all upload providers
- **AmazonS3Provider** / **CustomUploaderProvider** - Produce URLs

### 1.3 Key Pattern to Replicate
```
Local File → Wait for Ready → Clone TaskSettings → TaskManager → WorkerTask → Provider → URL
```

**Rule**: Mobile must call `TaskManager` the same way desktop does.

---

## Phase 2: Create ShareImportService in XerahS.Core

Create a platform-neutral service with no UI dependencies.

### 2.1 Service Definition

`src/XerahS.Core/Services/ShareImportService.cs`:

```csharp
namespace XerahS.Core.Services;

public sealed class ShareImportService
{
    public async Task<IReadOnlyList<ShareImportResult>> ImportAndUploadAsync(
        IReadOnlyList<string> localFilePaths,
        CancellationToken token = default)
    {
        var results = new List<ShareImportResult>();
        
        foreach (var path in localFilePaths)
        {
            try
            {
                // Wait for file to be ready
                await WaitForFileReadyAsync(path, token);
                
                // Get workflow from settings
                var settings = GetWorkflowSettings();
                
                // Clone TaskSettings
                var uploadSettings = TaskSettings.Clone(settings);
                uploadSettings.Job = WorkflowType.FileUpload;
                
                // Optionally move to screenshots folder
                if (SettingsManager.Settings.ShareImport.MoveToScreenshotsFolder)
                {
                    path = MoveToScreenshotsFolder(path, uploadSettings);
                }
                
                // Start upload via TaskManager
                var result = await TaskManager.Instance.StartFileTask(uploadSettings, path);
                
                results.Add(new ShareImportResult(
                    path, 
                    Success: result != null && !string.IsNullOrEmpty(result.URL),
                    result?.URL,
                    Error: result?.Errors?.FirstOrDefault()?.Message));
            }
            catch (Exception ex)
            {
                results.Add(new ShareImportResult(path, false, null, ex.Message));
            }
        }
        
        return results;
    }
    
    private static async Task WaitForFileReadyAsync(string path, CancellationToken token, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            token.ThrowIfCancellationRequested();
            
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100, token);
            }
        }
    }
    
    private TaskSettings GetWorkflowSettings()
    {
        var workflowId = SettingsManager.Settings.ShareImport.ShareImportWorkflowId;
        return SettingsManager.GetWorkflowById(workflowId) ?? SettingsManager.GetDefaultTaskSettings();
    }
}

public sealed record ShareImportResult(
    string LocalPath,
    bool Success,
    string? Url,
    string? Error);
```

### 2.2 Rules
- This service must not know about Android or iOS
- This service must not know about Avalonia views
- This service must not call providers directly
- This service must not contain HTTP logic

---

## Phase 3: Add Settings for Share Import

### 3.1 Settings Model

`src/XerahS.Core/Models/ShareImportSettings.cs`:

```csharp
namespace XerahS.Core.Models;

public class ShareImportSettings
{
    public bool ShareImportEnabled { get; set; } = true;
    public string ShareImportWorkflowId { get; set; } = "default";
    public bool MoveToScreenshotsFolder { get; set; } = true;
    public bool CopyUrlToClipboard { get; set; } = true;
    public bool ShowResultToast { get; set; } = true;
}
```

### 3.2 Extend SettingsManager

Add to `SettingsManager.Settings`:

```csharp
public ShareImportSettings ShareImport { get; set; } = new();
```

### 3.3 Rules
- Reuse workflow selection UI patterns
- Do not create new destination selection system
- Map workflow ID to existing workflow entry

---

## Phase 4: Implement Android Share to XerahS

### 4.1 Update Android Manifest

`src/XerahS.App/Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <application 
    android:label="XerahS" 
    android:icon="@drawable/icon"
    android:name="android.app.Application">
    
    <activity 
      android:name="mainactivity"
      android:exported="true"
      android:launchMode="singleTop">
      <intent-filter>
        <action android:name="android.intent.action.MAIN" />
        <category android:name="android.intent.category.LAUNCHER" />
      </intent-filter>
      
      <!-- Share intent filters -->
      <intent-filter>
        <action android:name="android.intent.action.SEND" />
        <action android:name="android.intent.action.SEND_MULTIPLE" />
        <category android:name="android.intent.category.DEFAULT" />
        <data android:mimeType="image/*" />
        <data android:mimeType="application/*" />
        <data android:mimeType="text/*" />
      </intent-filter>
    </activity>
  </application>
  
  <uses-permission android:name="android.permission.INTERNET" />
</manifest>
```

### 4.2 Handle Share Intent in MainActivity

`src/XerahS.App/Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Avalonia.Android;
using XerahS.Core.Services;

namespace XerahS.App;

[Activity(
    Label = "XerahS",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        PlatformServices.InitializeMobile();
        
        // Handle share intent
        HandleShareIntent(Intent);
    }
    
    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        HandleShareIntent(intent);
    }
    
    private async void HandleShareIntent(Intent intent)
    {
        if (intent?.Action != Intent.ActionSend && 
            intent?.Action != Intent.ActionSendMultiple)
            return;
        
        var localPaths = await ExtractSharedFilesAsync(intent);
        if (localPaths.Count == 0) return;
        
        // Store for processing by UI
        ShareIntentData.PendingPaths = localPaths;
    }
    
    private async Task<List<string>> ExtractSharedFilesAsync(Intent intent)
    {
        var paths = new List<string>();
        
        if (intent.Action == Intent.ActionSend)
        {
            var uri = intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;
            if (uri != null)
            {
                var path = await CopyUriToLocalAsync(uri);
                if (path != null) paths.Add(path);
            }
        }
        else if (intent.Action == Intent.ActionSendMultiple)
        {
            var uris = intent.GetParcelableArrayListExtra(Intent.ExtraStream);
            if (uris != null)
            {
                foreach (Android.Net.Uri uri in uris)
                {
                    var path = await CopyUriToLocalAsync(uri);
                    if (path != null) paths.Add(path);
                }
            }
        }
        
        return paths;
    }
    
    private async Task<string?> CopyUriToLocalAsync(Android.Net.Uri uri)
    {
        try
        {
            var fileName = GetFileNameFromUri(uri) ?? $"shared_{Guid.NewGuid()}.bin";
            var destPath = System.IO.Path.Combine(CacheDir!.AbsolutePath, fileName);
            
            await using var input = ContentResolver!.OpenInputStream(uri);
            await using var output = File.OpenWrite(destPath);
            await input!.CopyToAsync(output);
            
            return destPath;
        }
        catch
        {
            return null;
        }
    }
    
    private string? GetFileNameFromUri(Android.Net.Uri uri)
    {
        string? fileName = null;
        
        using var cursor = ContentResolver?.Query(uri, null, null, null, null);
        if (cursor?.MoveToFirst() == true)
        {
            var nameIndex = cursor.GetColumnIndex(OpenableColumns.DisplayName);
            if (nameIndex >= 0)
            {
                fileName = cursor.GetString(nameIndex);
            }
        }
        
        return fileName;
    }
}

public static class ShareIntentData
{
    public static List<string>? PendingPaths { get; set; }
}
```

### 4.3 Rules
- No upload code in Android head project
- Android head only converts share payload into local files
- Pass local paths to `ShareImportService`

---

## Phase 5: Implement iOS Share Extension

### 5.1 Create App Group

Create App Group identifier: `group.com.sharexteam.xerahs`

Enable for:
- Main iOS app target
- Share Extension target

### 5.2 Create Share Extension Project

`src/XerahS.ShareExtension.iOS/XerahS.ShareExtension.iOS.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Library</OutputType>
    <IsAppExtension>true</IsAppExtension>
  </PropertyGroup>
</Project>
```

### 5.3 Share Extension ViewController

`src/XerahS.ShareExtension.iOS/ShareViewController.cs`:

```csharp
using Foundation;
using MobileCoreServices;
using Social;
using UniformTypeIdentifiers;

namespace XerahS.ShareExtension.iOS;

public partial class ShareViewController : SLComposeServiceViewController
{
    protected ShareViewController(IntPtr handle) : base(handle) {}
    
    public override async void DidSelectPost()
    {
        var extensionItem = ExtensionContext?.InputItems.FirstOrDefault() as NSExtensionItem;
        var attachments = extensionItem?.Attachments ?? Array.Empty<NSItemProvider>();
        
        var handoffItems = new List<HandoffItem>();
        
        foreach (var attachment in attachments)
        {
            if (attachment.HasItemConformingTo(UTTypes.Image.Identifier))
            {
                var data = await LoadItemAsync(attachment, UTTypes.Image.Identifier);
                if (data != null)
                {
                    var fileName = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var relativePath = $"incoming/{fileName}";
                    var fullPath = Path.Combine(GetAppGroupPath(), relativePath);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    await File.WriteAllBytesAsync(fullPath, data);
                    
                    handoffItems.Add(new HandoffItem(relativePath));
                }
            }
            // Handle other types...
        }
        
        // Write handoff manifest
        if (handoffItems.Count > 0)
        {
            var manifest = new HandoffManifest(
                DateTime.UtcNow,
                handoffItems);
            
            var manifestName = $"handoff_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var manifestPath = Path.Combine(GetAppGroupPath(), "manifests", manifestName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
        }
        
        // Signal completion and open main app
        await OpenMainAppAsync();
        ExtensionContext?.CompleteRequest(null, null);
    }
    
    private async Task<byte[]?> LoadItemAsync(NSItemProvider provider, string typeIdentifier)
    {
        var tcs = new TaskCompletionSource<byte[]?>();
        
        provider.LoadItem(typeIdentifier, null, (item, error) =>
        {
            if (item is NSData data)
            {
                tcs.SetResult(data.ToArray());
            }
            else if (item is NSUrl url)
            {
                tcs.SetResult(File.ReadAllBytes(url.Path!));
            }
            else
            {
                tcs.SetResult(null);
            }
        });
        
        return await tcs.Task;
    }
    
    private static string GetAppGroupPath()
    {
        var containerUrl = NSFileManager.DefaultManager.GetContainerUrl("group.com.sharexteam.xerahs");
        return containerUrl!.Path!;
    }
    
    private async Task OpenMainAppAsync()
    {
        var url = new NSUrl("xerahs://share");
        await UIApplication.SharedApplication.OpenUrlAsync(url, new UIApplicationOpenUrlOptions());
    }
}

public record HandoffManifest(DateTime CreatedUtc, List<HandoffItem> Items);
public record HandoffItem(string RelativePath);
```

### 5.4 Main iOS App Process Handoff Manifests

`src/XerahS.App/Platforms/iOS/HandoffProcessor.cs`:

```csharp
using Foundation;
using XerahS.Core.Services;

namespace XerahS.App.Platforms.iOS;

public class HandoffProcessor
{
    private readonly ShareImportService _importService;
    private readonly string _appGroupPath;
    
    public HandoffProcessor(ShareImportService importService)
    {
        _importService = importService;
        _appGroupPath = NSFileManager.DefaultManager
            .GetContainerUrl("group.com.sharexteam.xerahs")!.Path!;
    }
    
    public async Task<List<ShareImportResult>> ProcessPendingManifestsAsync()
    {
        var results = new List<ShareImportResult>();
        var manifestDir = Path.Combine(_appGroupPath, "manifests");
        
        if (!Directory.Exists(manifestDir)) return results;
        
        var manifestFiles = Directory.GetFiles(manifestDir, "handoff_*.json");
        
        foreach (var manifestPath in manifestFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<HandoffManifest>(json);
                
                if (manifest?.Items != null)
                {
                    var localPaths = manifest.Items
                        .Select(i => Path.Combine(_appGroupPath, i.RelativePath))
                        .Where(File.Exists)
                        .ToList();
                    
                    if (localPaths.Count > 0)
                    {
                        var uploadResults = await _importService.ImportAndUploadAsync(localPaths);
                        results.AddRange(uploadResults);
                    }
                }
                
                // Delete processed manifest
                File.Delete(manifestPath);
                
                // Clean up imported files
                foreach (var item in manifest?.Items ?? new List<HandoffItem>())
                {
                    var path = Path.Combine(_appGroupPath, item.RelativePath);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            catch
            {
                // Move to failed folder for inspection
                var failedDir = Path.Combine(_appGroupPath, "failed");
                Directory.CreateDirectory(failedDir);
                var failedPath = Path.Combine(failedDir, Path.GetFileName(manifestPath));
                File.Move(manifestPath, failedPath, true);
            }
        }
        
        return results;
    }
}
```

### 5.5 Rules
- Extension must not upload
- Extension must not load full XerahS.Core
- Upload happens only in main app
- Keep retries safe and idempotent

---

## Phase 6: Avalonia UI for Share Results

### 6.1 Share Results ViewModel

`src/XerahS.App/ViewModels/ShareImportResultsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core.Services;

namespace XerahS.App.ViewModels;

public partial class ShareImportResultsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ShareResultItem> _results = new();
    
    [RelayCommand]
    private async Task CopyUrlAsync(string url)
    {
        await ClipboardService.SetTextAsync(url);
    }
    
    [RelayCommand]
    private async Task ShareUrlAsync(string url)
    {
        await PlatformServices.ShareAsync(url);
    }
    
    public void LoadResults(IReadOnlyList<ShareImportResult> results)
    {
        Results.Clear();
        foreach (var result in results)
        {
            Results.Add(new ShareResultItem
            {
                FileName = Path.GetFileName(result.LocalPath),
                Status = result.Success ? "Success" : "Failed",
                Url = result.Url,
                Error = result.Error,
                ShowCopyButton = result.Success && !string.IsNullOrEmpty(result.Url)
            });
        }
    }
}

public class ShareResultItem
{
    public string FileName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Url { get; set; }
    public string? Error { get; set; }
    public bool ShowCopyButton { get; set; }
}
```

### 6.2 Share Results View

`src/XerahS.App/Views/ShareImportResultsView.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="XerahS.App.Views.ShareImportResultsView"
        Title="Share Results"
        Width="500"
        Height="600">
  <Grid RowDefinitions="Auto,*,Auto">
    <TextBlock Grid.Row="0" 
               Text="Upload Results" 
               FontSize="20" 
               FontWeight="Bold"
               Margin="16"/>
    
    <ScrollViewer Grid.Row="1">
      <ItemsControl ItemsSource="{Binding Results}" Margin="16,0">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Classes="card" Margin="0,8">
              <StackPanel>
                <TextBlock Text="{Binding FileName}" FontWeight="SemiBold"/>
                <TextBlock Text="{Binding Status}" 
                           Foreground="{Binding Status, Converter={StaticResource StatusToBrushConverter}}"/>
                
                <TextBlock Text="{Binding Url}" 
                           IsVisible="{Binding ShowCopyButton}"
                           TextTrimming="CharacterEllipsis"
                           Foreground="{DynamicResource ThemeAccentBrush}"/>
                
                <TextBlock Text="{Binding Error}" 
                           IsVisible="{Binding Error, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                           Foreground="Red"/>
                
                <StackPanel Orientation="Horizontal" 
                            IsVisible="{Binding ShowCopyButton}"
                            Spacing="8">
                  <Button Content="Copy URL" 
                          Command="{Binding CopyUrlCommand}" 
                          CommandParameter="{Binding Url}"/>
                  <Button Content="Share" 
                          Command="{Binding ShareUrlCommand}" 
                          CommandParameter="{Binding Url}"/>
                </StackPanel>
              </StackPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
    
    <Button Grid.Row="2" 
            Content="Close" 
            Command="{Binding CloseCommand}"
            HorizontalAlignment="Right"
            Margin="16"/>
  </Grid>
</Window>
```

### 6.3 Rules
- No platform-specific UI in the view
- Use abstraction services for clipboard and share actions
- Works on mobile and can be used on desktop later

---

## Phase 7: Platform Abstractions

### 7.1 Clipboard Service

`src/XerahS.Platform.Abstractions/Services/IClipboardService.cs`:

```csharp
namespace XerahS.Platform.Abstractions.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string?> GetTextAsync();
}
```

### 7.2 Share Service

```csharp
namespace XerahS.Platform.Abstractions.Services;

public interface IShareService
{
    Task ShareAsync(string text, string? title = null);
}
```

### 7.3 Rules
- Core service should not call clipboard or share actions
- UI layer calls them after receiving results

---

## Phase 8: Security and Privacy

- **Do not monitor screenshots automatically**
- **Only process files explicitly shared by the user**
- **Do not request photo library permissions** just to find screenshots
- Credentials stored using existing secret store patterns
- Implement platform-specific secret stores if needed
- Do not embed secrets in config JSON

---

## Phase 9: Testing Plan

### Android Tests
- [ ] Share one screenshot from Photos app to XerahS
- [ ] Share multiple images
- [ ] Share a PDF or ZIP
- [ ] Verify files are copied locally then uploaded
- [ ] Verify URL returned and can be copied
- [ ] Test with low storage
- [ ] Test network offline conditions

### iOS Tests
- [ ] Share one screenshot from Photos to XerahS share extension
- [ ] Share multiple images
- [ ] Validate App Group handoff works
- [ ] Validate main app processes manifests on launch/resume
- [ ] Validate URL shown
- [ ] Validate no duplicate uploads on repeated processing

### Pipeline Validation
- [ ] Confirm ShareImportService triggers TaskManager and providers
- [ ] Confirm AmazonS3Provider and CustomUploader produce URL
- [ ] Confirm no duplicate upload code in mobile heads or extension

---

## Phase 10: Deliverables

1. **ShareImportService** in XerahS.Core
2. **Settings additions** for Share import workflow selection
3. **Android share intent handling** - copies shared content, calls ShareImportService
4. **iOS Share Extension** - copies items to App Group, writes handoff manifest
5. **iOS main app processing** - processes manifests, calls ShareImportService
6. **Avalonia UI** for showing upload results and URLs
7. **Documentation** under docs/ covering setup, App Group configuration, user flow

---

## Phase 11: Non-Negotiable Rules

- Do not create a second upload pipeline
- Do not call Amazon S3 or CustomUploader directly from mobile heads or extension
- Do not duplicate TaskSettings cloning logic
- Do not duplicate file naming logic
- Ensure every upload goes through TaskManager and existing provider infrastructure
- Keep platform-specific code isolated in platform head projects or iOS share extension only

---

## End State

User shares a screenshot to XerahS → XerahS uploads using configured workflow and destination → User receives a URL inside the XerahS UI

---

## Architecture Summary

```
User Shares File
    ↓
┌─────────────────┐     ┌─────────────────────┐
│ Android Intent  │     │ iOS Share Extension │
│  (Content URI)  │     │   (NSItemProvider)  │
└────────┬────────┘     └──────────┬──────────┘
         │                         │
    Copy to local              Copy to App Group
    storage (Cache)            + Write manifest
         │                         │
         └───────────┬─────────────┘
                     ↓
         ┌───────────────────────┐
         │   ShareImportService  │  ← XerahS.Core
         │  (platform neutral)   │
         └───────────┬───────────┘
                     ↓
         ┌───────────────────────┐
         │     TaskManager       │  ← XerahS.Core
         │   .StartFileTask()    │
         └───────────┬───────────┘
                     ↓
         ┌───────────────────────┐
         │     WorkerTask        │  ← XerahS.Core
         │    UploadJobProcessor │
         └───────────┬───────────┘
                     ↓
         ┌───────────────────────┐
         │  Provider (S3/Custom) │  ← XerahS.Uploaders
         └───────────┬───────────┘
                     ↓
                UploadResult.URL
                     ↓
         ┌───────────────────────┐
         │  ShareImportResults   │  ← Avalonia UI
         │       Window          │
         └───────────────────────┘
```
