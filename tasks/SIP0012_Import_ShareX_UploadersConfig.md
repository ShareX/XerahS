# CX06: Import ShareX UploadersConfig.json

## Priority
**MEDIUM** - Enables seamless migration from ShareX to ShareX Avalonia

## Assignee
**Codex** (Surface Laptop 5, VS Code)

## Branch
`feature/import-uploaders-config`

## Instructions
**CRITICAL**: Create the `feature/import-uploaders-config` branch first before starting work.

```bash
git checkout master
git pull origin master
git checkout -b feature/import-uploaders-config
```

## Objective
Implement functionality to import `UploadersConfig.json` from ShareX (WinForms) into ShareX Avalonia, automatically populating matching uploader settings in Destination Settings. This enables users to migrate their existing uploader configurations without manual re-entry.

## Background
ShareX (WinForms) stores uploader configurations in `UploadersConfig.json`. ShareX Avalonia uses the same `UploadersConfig` class structure, making it possible to import settings directly. This task implements:
- **Phase 1**: Backend logic to locate and deserialize ShareX's `UploadersConfig.json`
- **Phase 2**: UI button in Destination Settings to trigger import
- **Phase 3**: Property mapping and validation to populate matching uploaders

## Scope

### Phase 1: Import Backend Logic

**File**: `src/ShareX.Avalonia.Uploaders/UploadersConfigImporter.cs` (NEW)

```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using ShareX.Ava.Common;

namespace ShareX.Ava.Uploaders;

/// <summary>
/// Handles importing UploadersConfig.json from ShareX (WinForms) into ShareX Avalonia
/// </summary>
public static class UploadersConfigImporter
{
    /// <summary>
    /// Default ShareX configuration directory
    /// </summary>
    private static string DefaultShareXConfigPath => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShareX");
    
    /// <summary>
    /// Locate ShareX UploadersConfig.json file
    /// </summary>
    public static string? FindShareXUploadersConfig()
    {
        // Check default ShareX location
        string defaultPath = Path.Combine(DefaultShareXConfigPath, "UploadersConfig.json");
        if (File.Exists(defaultPath))
        {
            DebugHelper.WriteLine($"Found ShareX UploadersConfig at: {defaultPath}");
            return defaultPath;
        }
        
        // Check portable mode (ShareX.exe directory)
        string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadersConfig.json");
        if (File.Exists(portablePath))
        {
            DebugHelper.WriteLine($"Found ShareX UploadersConfig (portable) at: {portablePath}");
            return portablePath;
        }
        
        DebugHelper.WriteLine("ShareX UploadersConfig.json not found in default locations");
        return null;
    }
    
    /// <summary>
    /// Import UploadersConfig from ShareX file
    /// </summary>
    /// <param name="sourceFilePath">Path to ShareX UploadersConfig.json</param>
    /// <param name="targetConfig">Target UploadersConfig to populate</param>
    /// <returns>Number of settings imported</returns>
    public static ImportResult ImportFromFile(string sourceFilePath, UploadersConfig targetConfig)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("UploadersConfig.json not found", sourceFilePath);
        }
        
        try
        {
            // Read and deserialize ShareX config
            string json = File.ReadAllText(sourceFilePath);
            var sourceConfig = JsonConvert.DeserializeObject<UploadersConfig>(json);
            
            if (sourceConfig == null)
            {
                throw new InvalidDataException("Failed to deserialize UploadersConfig.json");
            }
            
            // Import settings
            var result = new ImportResult();
            ImportImageUploaders(sourceConfig, targetConfig, result);
            ImportTextUploaders(sourceConfig, targetConfig, result);
            ImportFileUploaders(sourceConfig, targetConfig, result);
            ImportUrlShorteners(sourceConfig, targetConfig, result);
            
            DebugHelper.WriteLine($"Import complete: {result.TotalImported} settings imported");
            return result;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Import failed: {ex.Message}");
            throw;
        }
    }
    
    private static void ImportImageUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
    {
        // Imgur
        if (source.ImgurOAuth2Info != null)
        {
            target.ImgurAccountType = source.ImgurAccountType;
            target.ImgurDirectLink = source.ImgurDirectLink;
            target.ImgurThumbnailType = source.ImgurThumbnailType;
            target.ImgurUseGIFV = source.ImgurUseGIFV;
            target.ImgurOAuth2Info = source.ImgurOAuth2Info;
            target.ImgurUploadSelectedAlbum = source.ImgurUploadSelectedAlbum;
            target.ImgurSelectedAlbum = source.ImgurSelectedAlbum;
            target.ImgurAlbumList = source.ImgurAlbumList;
            result.AddImported("Imgur");
        }
        
        // ImageShack
        if (source.ImageShackSettings != null)
        {
            target.ImageShackSettings = source.ImageShackSettings;
            result.AddImported("ImageShack");
        }
        
        // Flickr
        if (source.FlickrOAuthInfo != null)
        {
            target.FlickrOAuthInfo = source.FlickrOAuthInfo;
            target.FlickrSettings = source.FlickrSettings;
            result.AddImported("Flickr");
        }
        
        // Photobucket
        if (source.PhotobucketOAuthInfo != null)
        {
            target.PhotobucketOAuthInfo = source.PhotobucketOAuthInfo;
            target.PhotobucketAccountInfo = source.PhotobucketAccountInfo;
            result.AddImported("Photobucket");
        }
        
        // Chevereto
        if (!string.IsNullOrEmpty(source.CheveretoUploader?.UploadURL))
        {
            target.CheveretoUploader = source.CheveretoUploader;
            target.CheveretoDirectURL = source.CheveretoDirectURL;
            result.AddImported("Chevereto");
        }
        
        // vgy.me
        if (!string.IsNullOrEmpty(source.VgymeUserKey))
        {
            target.VgymeUserKey = source.VgymeUserKey;
            result.AddImported("vgy.me");
        }
    }
    
    private static void ImportTextUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
    {
        // Pastebin
        if (source.PastebinSettings != null && !string.IsNullOrEmpty(source.PastebinSettings.UserKey))
        {
            target.PastebinSettings = source.PastebinSettings;
            result.AddImported("Pastebin");
        }
        
        // Paste.ee
        if (!string.IsNullOrEmpty(source.Paste_eeUserKey))
        {
            target.Paste_eeUserKey = source.Paste_eeUserKey;
            target.Paste_eeEncryptPaste = source.Paste_eeEncryptPaste;
            result.AddImported("Paste.ee");
        }
        
        // Gist
        if (source.GistOAuth2Info != null)
        {
            target.GistOAuth2Info = source.GistOAuth2Info;
            target.GistPublishPublic = source.GistPublishPublic;
            target.GistRawURL = source.GistRawURL;
            target.GistCustomURL = source.GistCustomURL;
            result.AddImported("Gist");
        }
        
        // uPaste
        if (!string.IsNullOrEmpty(source.UpasteUserKey))
        {
            target.UpasteUserKey = source.UpasteUserKey;
            target.UpasteIsPublic = source.UpasteIsPublic;
            result.AddImported("uPaste");
        }
        
        // Hastebin
        if (!string.IsNullOrEmpty(source.HastebinCustomDomain))
        {
            target.HastebinCustomDomain = source.HastebinCustomDomain;
            target.HastebinSyntaxHighlighting = source.HastebinSyntaxHighlighting;
            target.HastebinUseFileExtension = source.HastebinUseFileExtension;
            result.AddImported("Hastebin");
        }
        
        // OneTimeSecret
        if (!string.IsNullOrEmpty(source.OneTimeSecretAPIKey))
        {
            target.OneTimeSecretAPIUsername = source.OneTimeSecretAPIUsername;
            target.OneTimeSecretAPIKey = source.OneTimeSecretAPIKey;
            result.AddImported("OneTimeSecret");
        }
    }
    
    private static void ImportFileUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
    {
        // Dropbox
        if (source.DropboxOAuth2Info != null)
        {
            target.DropboxOAuth2Info = source.DropboxOAuth2Info;
            target.DropboxUploadPath = source.DropboxUploadPath;
            target.DropboxAutoCreateShareableLink = source.DropboxAutoCreateShareableLink;
            target.DropboxUseDirectLink = source.DropboxUseDirectLink;
            result.AddImported("Dropbox");
        }
        
        // FTP
        if (source.FTPAccountList != null && source.FTPAccountList.Count > 0)
        {
            target.FTPAccountList = source.FTPAccountList;
            target.FTPSelectedImage = source.FTPSelectedImage;
            target.FTPSelectedText = source.FTPSelectedText;
            target.FTPSelectedFile = source.FTPSelectedFile;
            result.AddImported("FTP");
        }
        
        // OneDrive
        if (source.OneDriveV2OAuth2Info != null)
        {
            target.OneDriveV2OAuth2Info = source.OneDriveV2OAuth2Info;
            target.OneDriveV2SelectedFolder = source.OneDriveV2SelectedFolder;
            target.OneDriveAutoCreateShareableLink = source.OneDriveAutoCreateShareableLink;
            target.OneDriveUseDirectLink = source.OneDriveUseDirectLink;
            result.AddImported("OneDrive");
        }
        
        // Google Drive
        if (source.GoogleDriveOAuth2Info != null)
        {
            target.GoogleDriveOAuth2Info = source.GoogleDriveOAuth2Info;
            target.GoogleDriveUserInfo = source.GoogleDriveUserInfo;
            target.GoogleDriveIsPublic = source.GoogleDriveIsPublic;
            target.GoogleDriveDirectLink = source.GoogleDriveDirectLink;
            target.GoogleDriveUseFolder = source.GoogleDriveUseFolder;
            target.GoogleDriveFolderID = source.GoogleDriveFolderID;
            target.GoogleDriveSelectedDrive = source.GoogleDriveSelectedDrive;
            result.AddImported("Google Drive");
        }
        
        // Amazon S3
        if (source.AmazonS3Settings != null && !string.IsNullOrEmpty(source.AmazonS3Settings.AccessKeyID))
        {
            target.AmazonS3Settings = source.AmazonS3Settings;
            result.AddImported("Amazon S3");
        }
        
        // Azure Storage
        if (!string.IsNullOrEmpty(source.AzureStorageAccountName))
        {
            target.AzureStorageAccountName = source.AzureStorageAccountName;
            target.AzureStorageAccountAccessKey = source.AzureStorageAccountAccessKey;
            target.AzureStorageContainer = source.AzureStorageContainer;
            target.AzureStorageEnvironment = source.AzureStorageEnvironment;
            target.AzureStorageCustomDomain = source.AzureStorageCustomDomain;
            target.AzureStorageUploadPath = source.AzureStorageUploadPath;
            target.AzureStorageCacheControl = source.AzureStorageCacheControl;
            result.AddImported("Azure Storage");
        }
        
        // Backblaze B2
        if (!string.IsNullOrEmpty(source.B2ApplicationKeyId))
        {
            target.B2ApplicationKeyId = source.B2ApplicationKeyId;
            target.B2ApplicationKey = source.B2ApplicationKey;
            target.B2BucketName = source.B2BucketName;
            target.B2UploadPath = source.B2UploadPath;
            target.B2UseCustomUrl = source.B2UseCustomUrl;
            target.B2CustomUrl = source.B2CustomUrl;
            result.AddImported("Backblaze B2");
        }
    }
    
    private static void ImportUrlShorteners(UploadersConfig source, UploadersConfig target, ImportResult result)
    {
        // bit.ly
        if (source.BitlyOAuth2Info != null)
        {
            target.BitlyOAuth2Info = source.BitlyOAuth2Info;
            target.BitlyDomain = source.BitlyDomain;
            result.AddImported("bit.ly");
        }
        
        // YOURLS
        if (!string.IsNullOrEmpty(source.YourlsAPIURL))
        {
            target.YourlsAPIURL = source.YourlsAPIURL;
            target.YourlsSignature = source.YourlsSignature;
            target.YourlsUsername = source.YourlsUsername;
            target.YourlsPassword = source.YourlsPassword;
            result.AddImported("YOURLS");
        }
        
        // Polr
        if (!string.IsNullOrEmpty(source.PolrAPIHostname))
        {
            target.PolrAPIHostname = source.PolrAPIHostname;
            target.PolrAPIKey = source.PolrAPIKey;
            target.PolrIsSecret = source.PolrIsSecret;
            target.PolrUseAPIv1 = source.PolrUseAPIv1;
            result.AddImported("Polr");
        }
        
        // Firebase Dynamic Links
        if (!string.IsNullOrEmpty(source.FirebaseWebAPIKey))
        {
            target.FirebaseWebAPIKey = source.FirebaseWebAPIKey;
            target.FirebaseDynamicLinkDomain = source.FirebaseDynamicLinkDomain;
            target.FirebaseIsShort = source.FirebaseIsShort;
            result.AddImported("Firebase Dynamic Links");
        }
        
        // Kutt
        if (source.KuttSettings != null && !string.IsNullOrEmpty(source.KuttSettings.APIKey))
        {
            target.KuttSettings = source.KuttSettings;
            result.AddImported("Kutt");
        }
    }
}

/// <summary>
/// Result of import operation
/// </summary>
public class ImportResult
{
    public List<string> ImportedUploaders { get; } = new List<string>();
    public int TotalImported => ImportedUploaders.Count;
    
    public void AddImported(string uploaderName)
    {
        ImportedUploaders.Add(uploaderName);
        DebugHelper.WriteLine($"  ✓ Imported: {uploaderName}");
    }
    
    public string GetSummary()
    {
        if (TotalImported == 0)
            return "No uploader settings found to import.";
        
        return $"Successfully imported {TotalImported} uploader(s):\n" + 
               string.Join("\n", ImportedUploaders.Select(u => $"  • {u}"));
    }
}
```

### Phase 2: UI Integration

**File**: `src/ShareX.Avalonia.UI/ViewModels/DestinationSettingsViewModel.cs`

Add import command:

```csharp
[RelayCommand]
private async Task ImportShareXConfig()
{
    try
    {
        // Try to find ShareX config automatically
        string? configPath = UploadersConfigImporter.FindShareXUploadersConfig();
        
        if (configPath == null)
        {
            // Show file picker if not found
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;
            
            if (topLevel == null) return;
            
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select ShareX UploadersConfig.json",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("ShareX Config")
                    {
                        Patterns = new[] { "UploadersConfig.json" }
                    },
                    new FilePickerFileType("JSON Files")
                    {
                        Patterns = new[] { "*.json" }
                    }
                }
            });
            
            if (files.Count == 0) return;
            configPath = files[0].Path.LocalPath;
        }
        
        // Import settings
        var result = UploadersConfigImporter.ImportFromFile(configPath, _uploadersConfig);
        
        // Save imported config
        _uploadersConfig.Save();
        
        // Refresh UI
        OnPropertyChanged(string.Empty); // Refresh all bindings
        
        // Show success message
        await ShowMessageDialog("Import Complete", result.GetSummary());
    }
    catch (Exception ex)
    {
        await ShowMessageDialog("Import Failed", $"Failed to import UploadersConfig:\n{ex.Message}");
    }
}

private async Task ShowMessageDialog(string title, string message)
{
    // TODO: Implement proper message dialog
    // For now, use DebugHelper
    DebugHelper.WriteLine($"[{title}] {message}");
}
```

**File**: `src/ShareX.Avalonia.UI/Views/DestinationSettingsView.axaml`

Add import button to the top of the view (find appropriate location in the existing layout):

```xml
<!-- Import ShareX Config Button -->
<Button Content="Import ShareX UploadersConfig"
        Command="{Binding ImportShareXConfigCommand}"
        HorizontalAlignment="Right"
        Margin="0,0,0,10"
        ToolTip.Tip="Import uploader settings from ShareX (WinForms) UploadersConfig.json"/>
```

### Phase 3: Custom Uploaders Import

**Enhancement to `UploadersConfigImporter.cs`**:

Add to `ImportFileUploaders` method:

```csharp
// Custom Uploaders
if (source.CustomUploadersList != null && source.CustomUploadersList.Count > 0)
{
    target.CustomUploadersList = source.CustomUploadersList;
    target.CustomImageUploaderSelected = source.CustomImageUploaderSelected;
    target.CustomTextUploaderSelected = source.CustomTextUploaderSelected;
    target.CustomFileUploaderSelected = source.CustomFileUploaderSelected;
    target.CustomURLShortenerSelected = source.CustomURLShortenerSelected;
    target.CustomURLSharingServiceSelected = source.CustomURLSharingServiceSelected;
    result.AddImported($"Custom Uploaders ({source.CustomUploadersList.Count})");
}
```

## Guidelines
- **Null-safe imports**: Only import settings if source data is valid (non-null, non-empty)
- **Preserve existing settings**: Import should merge, not replace all settings
- **Validation**: Validate OAuth tokens and API keys are present before importing
- **Debug logging**: Log each imported uploader for troubleshooting
- **Error handling**: Clear error messages if file not found or deserialization fails
- **Auto-save**: Save `UploadersConfig` immediately after successful import

## Don't Worry About
- Selective import (choosing specific uploaders) - import all available
- Conflict resolution - overwrite existing settings with imported ones
- Import history/undo - future enhancement
- Encrypted field decryption - Newtonsoft.Json handles `[JsonEncrypt]` attributes

## Deliverables
- ✅ `UploadersConfigImporter.cs` with import logic for all uploader categories
- ✅ `ImportResult` class to track imported uploaders
- ✅ `ImportShareXConfigCommand` in `DestinationSettingsViewModel`
- ✅ "Import ShareX UploadersConfig" button in `DestinationSettingsView.axaml`
- ✅ Build succeeds on `feature/import-uploaders-config`
- ✅ Manual test: Import real ShareX config successfully
- ✅ Commit and push changes

## Testing

### Manual Test

1. **Prepare test data**:
   - Locate your existing ShareX installation's `UploadersConfig.json`
   - Default location: `%USERPROFILE%\Documents\ShareX\UploadersConfig.json`
   - Or use a sample config with configured uploaders (Imgur, Dropbox, etc.)

2. **Test auto-detection**:
   - Run ShareX Avalonia
   - Navigate to Settings → Destination Settings
   - Click "Import ShareX UploadersConfig" button
   - Verify it finds the config automatically if in default location

3. **Test manual selection**:
   - Move `UploadersConfig.json` to a custom location
   - Click import button
   - Verify file picker opens
   - Select the config file
   - Verify import completes

4. **Verify imported settings**:
   - Check that uploader settings are populated (e.g., Imgur OAuth token)
   - Verify settings persist after app restart
   - Check Debug output for import summary

5. **Test error cases**:
   - Try importing invalid JSON file (should show error)
   - Try importing empty file (should show "No settings found")
   - Cancel file picker (should do nothing)

### Expected Debug Output
```
[UploadersConfigImporter] Found ShareX UploadersConfig at: C:\Users\...\Documents\ShareX\UploadersConfig.json
[UploadersConfigImporter]   ✓ Imported: Imgur
[UploadersConfigImporter]   ✓ Imported: Dropbox
[UploadersConfigImporter]   ✓ Imported: Google Drive
[UploadersConfigImporter]   ✓ Imported: FTP
[UploadersConfigImporter]   ✓ Imported: Custom Uploaders (3)
[UploadersConfigImporter] Import complete: 5 settings imported
```

## Estimated Effort
**Medium** - 3-4 hours
- Phase 1 (Backend): 2 hours
- Phase 2 (UI Integration): 1 hour
- Phase 3 (Custom Uploaders): 30 minutes
- Testing: 30 minutes

## Success Criteria
- User can click "Import ShareX UploadersConfig" button
- App automatically finds ShareX config in default location
- If not found, file picker allows manual selection
- All configured uploaders from ShareX are imported into ShareX Avalonia
- Settings persist after restart
- Clear feedback on what was imported

## Future Enhancements
- Selective import (checkboxes for each uploader)
- Import preview before applying
- Merge strategies (keep existing vs. overwrite)
- Export ShareX Avalonia config to ShareX format
