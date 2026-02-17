# Media Explorer — Rev0 Implementation Plan

**Date**: 2026-02-17
**Status**: Approved

---

## 1 – Codebase Orientation

No cloning is needed. The repository lives at `/home/xk/Documents/GitHub/XerahS`. Key projects relevant to the Media Explorer:

| Project | Path | Role |
|---------|------|------|
| `XerahS.Uploaders` | `src/XerahS.Uploaders/` | Core uploader contracts, plugin system, provider catalog |
| `XerahS.UI` | `src/XerahS.UI/` | Avalonia views (102 files) & viewmodels (51 files) — MVVM |
| `XerahS.History` | `src/XerahS.History/` | Local upload history (SQLite/JSON/XML backends) |
| `XerahS.Platform.Abstractions` | `src/XerahS.Platform.Abstractions/` | Cross-platform service interfaces |
| `XerahS.Core` | `src/XerahS.Core/` | Business logic, `TaskManager`, `SettingsManager` |
| **Plugins** | `src/Plugins/` | 5 provider plugins (AmazonS3, Imgur, Auto, GitHubGist, Paste2) |

### Build & Run

```bash
# From repo root
dotnet build XerahS.sln
dotnet run --project src/XerahS.App
```

---

## 2 – Plugin Architecture

The plugin system is already fully implemented as **pure dynamic loading**. Here is what exists today:

### Core Interfaces (in `src/XerahS.Uploaders/PluginSystem/`)

| File | Type | Purpose |
|------|------|---------|
| [IUploaderProvider.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/IUploaderProvider.cs) | Interface | Main provider contract — 8 members: `ProviderId`, `Name`, `Description`, `Version`, `SupportedCategories`, `CreateInstance()`, `CreateConfigView()`, `ConfigChanged` event, etc. |
| [UploaderProviderBase.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/UploaderProviderBase.cs) | Abstract class | Implements `IUploaderProvider` + `IProviderContextAware`. Gives plugins access to `ISecretStore` via `Context.Secrets`. |
| [IUploaderPlugin.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/IUploaderPlugin.cs) | Interface | Legacy single-category interface (still present, not actively used by new plugins). |
| [UploaderCategory.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/UploaderCategory.cs) | Enum | `Image`, `Text`, `File`, `UrlShortener`, `UrlSharing` |
| [UploaderInstance.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/UploaderInstance.cs) | Model | Configured instance: `InstanceId`, `ProviderId`, `Category`, `DisplayName`, `SettingsJson`, `FileTypeRouting` |
| [ProviderCatalog.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs) | Static class | Registry (497 lines). Key methods: `GetProvider(id)`, `GetAllProviders()`, `GetProvidersByCategory()`, `LoadPlugins()`, `RegisterProvider()`. Also loads `.sxcu` custom uploaders. |
| [PluginManifest.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/PluginManifest.cs) | Model | Deserialized from `plugin.json` — has `PluginId`, `ApiVersion`, `EntryPoint`, `SupportedCategories`, `Dependencies`, `HomepageUrl` |

### Plugin Loading Flow

```
App Startup → ProviderCatalog.LoadPlugins(dirs)
  → PluginDiscovery.DiscoverPlugins() scans for plugin.json
  → PluginLoader.LoadPlugin() uses isolated PluginLoadContext per assembly
  → Provider registered in ProviderCatalog._providers dictionary
  → IProviderContextAware.SetContext() injects ISecretStore
```

### Existing Provider Implementations

| Plugin | ProviderId | Categories | Notable |
|--------|-----------|------------|---------|
| [ShareX.AmazonS3.Plugin](file:///home/xk/Documents/GitHub/XerahS/src/Plugins/ShareX.AmazonS3.Plugin/) | `amazons3` | Image, Text, File | Has SSO auth, S3 signer — **best candidate for first explorer implementation** (S3 ListObjects API is well-suited) |
| [ShareX.Imgur.Plugin](file:///home/xk/Documents/GitHub/XerahS/src/Plugins/ShareX.Imgur.Plugin/) | `imgur` | Image | Already has `ImgurUploader.GetAlbums()` — a browsing-like method listing user albums. **Good second candidate.** |
| ShareX.GitHubGist.Plugin | `githubgist` | Text | Gist listing possible via GitHub API |
| ShareX.Paste2.Plugin | `paste2` | Text | Simple paste service, no browsing API |
| ShareX.Auto.Plugin | `auto` | Image, Text, File | Auto-routing provider, not a real storage backend |

> [!IMPORTANT]
> **Key finding**: `ImgurUploader` (line 144–182) already implements `GetAlbums()` with pagination — this proves the browsing pattern is feasible within the existing plugin architecture. We should reuse this pattern.

### What Does NOT Exist Yet

- ❌ No `IUploaderExplorer` or browsing interface
- ❌ No `MediaItem` model for remote file listings
- ❌ No `ProviderCatalog.GetExplorer()` method
- ❌ No UI for browsing remote provider content
- ❌ No `supportsExplorer` flag in `plugin.json` schema

---

## 3 – Cross-Platform Considerations

### Already in Place

- **Avalonia MVVM** — All UI lives in shared `XerahS.UI` project. The codebase uses `CommunityToolkit.Mvvm` with `[ObservableProperty]`, `[RelayCommand]`, and `ViewModelBase`.
- **Platform abstraction** — [ISystemService](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Platform.Abstractions/ISystemService.cs) provides `ShowFileInExplorer()`, `OpenUrl()`, `OpenFile()` with implementations for Windows, Linux, macOS, and Mobile.
- **IClipboardService** — Already available for Copy URL functionality.
- **5 platform projects** — Windows, Linux, macOS, Android, iOS — all with thin wrappers.

### Reference Pattern: HistoryView

The [HistoryViewModel](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.UI/ViewModels/HistoryViewModel.cs) (589 lines) is the **closest existing analogue** to what the Media Explorer will be. It already demonstrates:

- **Grid/List toggle** — `IsGridView` property with `ToggleView()` command
- **Pagination** — `CurrentPage`, `TotalPages`, `PageSize`, `NextPage()`, `PreviousPage()`
- **Async thumbnail loading** — `LoadThumbnailsInBackgroundAsync()` with `CancellationTokenSource`
- **Thumbnail converter** — `Bitmap.DecodeToWidth(stream, 180)` for memory-efficient thumbnails
- **Context menu actions** — `OpenFile`, `OpenFolder`, `CopyURL`, `CopyFilePath`, `DeleteItem`, `EditImage`, `UploadItem`
- **Delete confirmation dialog** — Programmatic Avalonia dialog

> [!TIP]
> The Media Explorer ViewModel should follow the `HistoryViewModel` patterns closely, but sourcing data from remote providers instead of local SQLite history.

---

## 4 – Media Explorer Requirements

From the user-provided mock-up, the explorer should support:

### Navigation
- Hierarchical breadcrumb path (e.g. `Root \ images \ 2026 \ 02`) with click-to-navigate
- Back/forward buttons; keyboard shortcuts (Alt+Left/Right on desktop, swipe gestures on mobile)

### Toolbar
- **Provider selector** — drop-down of configured `UploaderInstance` items that support browsing
- **Upload** button — trigger new upload to current path
- **Refresh** — re-call `ListAsync()` for current folder
- **Search** — text filter by filename/metadata
- **File type filter** — drop-down (Images, Videos, Text, All)
- **Sort order** — by date, name, size (ascending/descending)
- **Date range picker** — filter by upload date
- **Statistics** — storage used / cost (provider-dependent, optional)

### Main Content Area
- **Grid of thumbnail cards** — each showing image preview (or type icon fallback), filename, size, upload date
- Responsive columns: 4 on desktop, 2 on mobile
- Virtualized for large collections (Avalonia `ItemsRepeater`)

### Item Actions
- **Open/Preview** — inline preview for images/video; `ISystemService.OpenUrl()` for remote files
- **Copy URL** — to clipboard
- **Download** — save to local filesystem
- **Delete** — remove from remote provider (with confirmation dialog)
- **View Metadata** — show full details panel

---

## 5 – Provider-Side API Design

### New Interface: `IUploaderExplorer`

Create in `src/XerahS.Uploaders/PluginSystem/IUploaderExplorer.cs`:

```csharp
namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Optional interface for providers that support browsing remote files.
/// Providers implement this alongside IUploaderProvider to enable the Media Explorer.
/// </summary>
public interface IUploaderExplorer
{
    /// <summary>
    /// Whether this provider supports hierarchical folders
    /// </summary>
    bool SupportsFolders { get; }

    /// <summary>
    /// Lists files and folders at the given remote path.
    /// Pass null or empty for root.
    /// </summary>
    Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default);

    /// <summary>
    /// Returns a thumbnail image for the given item (JPEG/PNG bytes).
    /// Returns null if thumbnails are not supported.
    /// </summary>
    Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default);

    /// <summary>
    /// Returns the full content stream for preview or download.
    /// </summary>
    Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default);

    /// <summary>
    /// Deletes a remote file. Returns true on success.
    /// </summary>
    Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default);

    /// <summary>
    /// Creates a folder (only if SupportsFolders is true).
    /// </summary>
    Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default);
}
```

### New Models (in `src/XerahS.Uploaders/PluginSystem/`)

```csharp
// MediaItem.cs
public class MediaItem
{
    public string Id { get; set; } = string.Empty;          // Provider-specific unique ID
    public string Name { get; set; } = string.Empty;        // Display name
    public string Path { get; set; } = string.Empty;        // Full remote path
    public bool IsFolder { get; set; }
    public long SizeBytes { get; set; }
    public string? MimeType { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? Url { get; set; }                        // Direct link if available
    public string? ThumbnailUrl { get; set; }               // Provider thumbnail URL
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// ExplorerQuery.cs
public class ExplorerQuery
{
    public string? FolderPath { get; set; }
    public string? SearchText { get; set; }
    public string? FileTypeFilter { get; set; }             // e.g. "image/*", "text/*"
    public ExplorerSortField SortBy { get; set; } = ExplorerSortField.Date;
    public bool SortDescending { get; set; } = true;
    public int PageSize { get; set; } = 50;
    public string? ContinuationToken { get; set; }          // For cursor-based pagination (S3, etc.)
}

public enum ExplorerSortField { Name, Date, Size }

// ExplorerPage.cs
public class ExplorerPage
{
    public IReadOnlyList<MediaItem> Items { get; set; } = Array.Empty<MediaItem>();
    public string? ContinuationToken { get; set; }          // null = last page
    public int? TotalCount { get; set; }                    // null if unknown (S3 doesn't know totals)
}
```

### Design Decisions

> [!IMPORTANT]
> **Why a separate interface instead of extending `IUploaderProvider`:**
> - **Binary compatibility** — Existing 3rd-party plugins won't break. `IUploaderProvider` stays at API v1.0.
> - **Optional capability** — Not all providers can browse (e.g. Paste2, URL shorteners). The core detects `IUploaderExplorer` via `is` check at runtime.
> - **Cursor-based pagination** — `ExplorerQuery.ContinuationToken` supports both offset and cursor paradigms (S3 uses continuation tokens, Imgur uses page numbers).

---

## 6 – Core Contract Changes

### Files to Create

| File | Location |
|------|----------|
| `IUploaderExplorer.cs` | `src/XerahS.Uploaders/PluginSystem/` |
| `MediaItem.cs` | `src/XerahS.Uploaders/PluginSystem/` |
| `ExplorerQuery.cs` | `src/XerahS.Uploaders/PluginSystem/` |
| `ExplorerPage.cs` | `src/XerahS.Uploaders/PluginSystem/` |

### Files to Modify

#### [MODIFY] [ProviderCatalog.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs)

Add a new method:

```csharp
/// <summary>
/// Get explorer implementation for a provider, or null if not supported.
/// </summary>
public static IUploaderExplorer? GetExplorer(string providerId)
{
    lock (_lock)
    {
        if (_providers.TryGetValue(providerId, out var provider) && provider is IUploaderExplorer explorer)
        {
            return explorer;
        }
        return null;
    }
}

/// <summary>
/// Get all providers that support browsing.
/// </summary>
public static List<IUploaderProvider> GetBrowsableProviders()
{
    lock (_lock)
    {
        return _providers.Values
            .Where(p => p is IUploaderExplorer)
            .ToList();
    }
}
```

#### [MODIFY] [PluginManifest.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/PluginManifest.cs)

Add optional field:

```diff
+    /// <summary>
+    /// Whether this plugin supports the Media Explorer API (browsing remote files).
+    /// </summary>
+    public bool SupportsExplorer { get; set; } = false;
```

#### [MODIFY] [UploaderInstanceViewModel.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs)

Add new computed properties and a command to open the explorer:

```csharp
/// <summary>
/// True if this instance's provider implements IUploaderExplorer.
/// Controls visibility of the "Browse Files" button in the config panel.
/// </summary>
[ObservableProperty]
private bool _supportsExplorer;

/// <summary>
/// True if SupportsExplorer AND the provider has valid credentials configured.
/// Controls whether the "Browse Files" button is enabled (clickable).
/// Uses the existing ValidateSettings() to check credential readiness.
/// </summary>
public bool IsExplorerEnabled => SupportsExplorer 
    && ProviderCatalog.GetProvider(ProviderId)?.ValidateSettings(SettingsJson) == true;

[RelayCommand]
private void OpenExplorer()
{
    // Resolve IUploaderExplorer from ProviderCatalog
    var explorer = ProviderCatalog.GetExplorer(ProviderId);
    if (explorer == null) return;

    // Open ProviderExplorerView in a new window, passing the instance + explorer
    var vm = new ProviderExplorerViewModel(Instance, explorer);
    var window = new ProviderExplorerWindow { DataContext = vm };
    window.Show();
}
```

The `SupportsExplorer` flag is set during `InitializeConfigViewModel()` after the provider is resolved:

```csharp
// Inside InitializeConfigViewModel(), after provider is found:
SupportsExplorer = provider is IUploaderExplorer;
```

The `IsExplorerEnabled` property automatically re-evaluates whenever `SettingsJson` changes (existing `PropertyChanged` listener already triggers on config changes). We add a `NotifyPropertyChangedFor` attribute:

```diff
 [ObservableProperty]
+[NotifyPropertyChangedFor(nameof(IsExplorerEnabled))]
 private string _settingsJson = "{}";
```

---

## 7 – UI Plan

### Entry Point: "Browse Files" Button in DestinationSettingsView

> [!IMPORTANT]
> The Media Explorer is **not** launched from a standalone sidebar button. Instead, it is accessed **per-provider-instance** from within the existing [DestinationSettingsView.axaml](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.UI/Views/DestinationSettingsView.axaml).

The current config panel layout (right side, shown when an instance is selected) is:

```
Config Panel (Grid.Column="2")
├── Verification Status (warning/error banners)
├── Identity (Name, Provider, Instance ID)
├── File Type Configuration
├── Provider Settings (plugin-specific config view)
└── 🆕 Media Explorer Section  ← NEW
```

#### [MODIFY] [DestinationSettingsView.axaml](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.UI/Views/DestinationSettingsView.axaml)

Add a new section **after the Provider Settings** `StackPanel` (after line 199), visible only when the provider supports browsing:

```xml
<!-- Media Explorer -->
<StackPanel Spacing="12"
            IsVisible="{Binding SelectedCategory.SelectedInstance.SupportsExplorer}">
    <Separator/>
    <TextBlock Text="Media Explorer" FontWeight="SemiBold" FontSize="16"/>
    <TextBlock Text="Browse and manage files stored with this provider."
               Foreground="{DynamicResource TextFillColorSecondaryBrush}"
               TextWrapping="Wrap"/>
    <Button Content="📂 Browse Files"
            Command="{Binding SelectedCategory.SelectedInstance.OpenExplorerCommand}"
            IsEnabled="{Binding SelectedCategory.SelectedInstance.IsExplorerEnabled}"
            Classes="accent"
            HorizontalAlignment="Left"
            Padding="16,8"
            ToolTip.Tip="Open Media Explorer to browse remote files (requires valid credentials)"
            AutomationProperties.Name="Open Media Explorer"/>
    <TextBlock Text="Configure credentials above to enable browsing."
               Foreground="{DynamicResource TextFillColorTertiaryBrush}"
               FontSize="11"
               IsVisible="{Binding !SelectedCategory.SelectedInstance.IsExplorerEnabled}"/>
</StackPanel>
```

**Behavior:**
- **Visibility**: The entire section is hidden for providers that don't implement `IUploaderExplorer` (e.g., Paste2, URL shorteners). Controlled by `SupportsExplorer` on `UploaderInstanceViewModel`.
- **Enabled state**: The button is grayed out when the provider's `ValidateSettings(settingsJson)` returns `false` — meaning credentials are not configured or invalid. A hint label explains this to the user.
- **Click action**: Opens the `ProviderExplorerView` in a new window, pre-bound to the selected instance and its `IUploaderExplorer` implementation.

### New Files to Create

| File | Location |
|------|----------|
| `ProviderExplorerView.axaml` | `src/XerahS.UI/Views/` |
| `ProviderExplorerView.axaml.cs` | `src/XerahS.UI/Views/` |
| `ProviderExplorerViewModel.cs` | `src/XerahS.UI/ViewModels/` |
| `ProviderExplorerWindow.axaml` | `src/XerahS.UI/Views/` |
| `ProviderExplorerWindow.axaml.cs` | `src/XerahS.UI/Views/` |

### ViewModel Design

Follow the `HistoryViewModel` pattern (which already has the pagination, grid/list toggle, thumbnail loading, and action commands we need):

```
ProviderExplorerViewModel : ViewModelBase, IDisposable
├── Constructor args: UploaderInstance instance, IUploaderExplorer explorer
│
├── Properties
│   ├── ObservableCollection<MediaItem> Items
│   ├── bool IsGridView (toggle like HistoryViewModel)
│   ├── bool IsLoading
│   ├── bool IsLoadingThumbnails
│   ├── string CurrentPath
│   ├── string WindowTitle (e.g. "Amazon S3 — Screenshots")
│   ├── ObservableCollection<string> BreadcrumbParts
│   ├── string SearchText
│   ├── string SelectedFileTypeFilter
│   ├── ExplorerSortField SortBy
│   ├── bool SortDescending
│   ├── string? ContinuationToken (for infinite scroll / "Load More")
│   ├── UploaderInstance BoundInstance (the instance that launched this explorer)
│   └── IUploaderExplorer Explorer
│
├── Commands (all [RelayCommand])
│   ├── LoadItemsAsync()
│   ├── LoadMoreAsync() — appends next page
│   ├── NavigateToFolder(string path)
│   ├── NavigateUp()
│   ├── NavigateBack() / NavigateForward()
│   ├── RefreshAsync()
│   ├── SearchAsync()
│   ├── ToggleView()
│   ├── OpenItem(MediaItem)
│   ├── PreviewItem(MediaItem)
│   ├── CopyUrl(MediaItem)
│   ├── DownloadItem(MediaItem)
│   ├── DeleteItem(MediaItem) — with confirmation dialog
│   ├── CreateFolder()
│   └── UploadToCurrentPath()
│
├── Thumbnail Loading
│   └── LoadThumbnailsInBackgroundAsync() — same CancellationTokenSource pattern as HistoryViewModel
│
└── No provider selector needed — the explorer is already bound to a specific instance
```

> [!NOTE]
> Unlike the original plan where the explorer had its own provider selector dropdown, this design binds each explorer window to the specific instance that launched it. There is no need for a provider selector within the explorer because each provider instance gets its own "Browse Files" button.

### View Layout (AXAML — ProviderExplorerWindow)

```
┌─────────────────────────────────────────────────────┐
│ Amazon S3 — Screenshots             [🔄 Refresh]   │
├─────────────────────────────────────────────────────┤
│ 📁 Root > images > 2026 > 02    [⬆ Up] [⬅] [➡]    │
├─────────────────────────────────────────────────────┤
│ 🔍 [Search...] [Type ▼] [Sort ▼] [📋/🔲 View]      │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐           │
│  │ 📸   │  │ 📸   │  │ 📸   │  │ 📁   │           │
│  │thumb │  │thumb │  │thumb │  │folder│           │
│  │      │  │      │  │      │  │      │           │
│  ├──────┤  ├──────┤  ├──────┤  ├──────┤           │
│  │name  │  │name  │  │name  │  │name  │           │
│  │1.2MB │  │800KB │  │3.1MB │  │4 items│          │
│  │Feb 15│  │Feb 14│  │Feb 13│  │       │           │
│  └──────┘  └──────┘  └──────┘  └──────┘           │
│                                                     │
│              [Load More...]                         │
├─────────────────────────────────────────────────────┤
│ Status: 47 items | 12.3 MB total                    │
└─────────────────────────────────────────────────────┘
```

### Avalonia Controls to Use

- **`ItemsRepeater`** with `UniformGridLayout` — for the grid view with virtualization
- **`ItemsControl`** — for list view fallback
- **`BreadcrumbBar`** or custom `ItemsControl` with `StackPanel(Horizontal)` — for path navigation
- **`Image`** with `Bitmap.DecodeToWidth()` — for thumbnails (same pattern as `HistoryViewModel.ThumbnailConverter`)
- **`ContextMenu`** — bound to commands for item actions
- **`ProgressBar` / `ProgressRing`** — for loading states

---

## 8 – Sample Provider Implementations

### Phase 1: Amazon S3 Explorer

The S3 plugin is the ideal first target because S3's `ListObjectsV2` API maps directly to the explorer interface:

| Explorer Method | S3 API | Notes |
|-----------------|--------|-------|
| `ListAsync()` | `ListObjectsV2` with `Prefix` and `Delimiter=/` | `ContinuationToken` maps directly to S3's `NextContinuationToken` |
| `GetThumbnailAsync()` | `GetObject` + resize | Generate thumbnails on-the-fly for image objects; cache locally |
| `GetContentAsync()` | `GetObject` | Stream directly |
| `DeleteAsync()` | `DeleteObject` | Straightforward |
| `CreateFolderAsync()` | `PutObject` with trailing `/` | S3 "folder" convention |

#### File to Modify

- [AmazonS3Provider.cs](file:///home/xk/Documents/GitHub/XerahS/src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs) — add `IUploaderExplorer` implementation
- Consider creating `AmazonS3Explorer.cs` as a separate class to keep provider clean, then reference from provider

#### Implementation Sketch for S3

The existing `AmazonS3Uploader` already implements S3 request signing via `AwsS3Signer`. The explorer methods can reuse this infrastructure to make `GET /?list-type=2&prefix=...&delimiter=/` requests.

### Phase 2: Imgur Explorer

The Imgur plugin already has `ImgurUploader.GetAlbums()` (line 144–182 of [ImgurUploader.cs](file:///home/xk/Documents/GitHub/XerahS/src/Plugins/ShareX.Imgur.Plugin/ImgurUploader.cs)) which demonstrates:
- Authenticated API calls with OAuth2 bearer tokens
- JSON pagination with `page` and `perPage` parameters
- Deserialization of `ImgurAlbumData`

#### Mapping

| Explorer Method | Imgur API | Notes |
|-----------------|-----------|-------|
| `ListAsync(root)` | `GET /3/account/me/albums` | Returns albums as "folders" |
| `ListAsync(albumId)` | `GET /3/album/{id}/images` | Returns images in album |
| `GetThumbnailAsync()` | Use `ThumbnailURL` convention | `https://i.imgur.com/{id}m.jpg` — already computed in uploader |
| `DeleteAsync()` | `DELETE /3/image/{deleteHash}` | Uses `deleteHash` from upload response |
| `SupportsFolders` | `true` | Albums act as folders |

---

## 9 – Thumbnail Caching Strategy

### Two-Level Cache

1. **In-memory** — `ConcurrentDictionary<string, byte[]>` keyed by `MediaItem.Id`, capped at ~200 entries with LRU eviction
2. **Disk cache** — Store thumbnails in `{AppDataDir}/Cache/Thumbnails/{ProviderId}/{ItemId}.jpg`
   - Use the existing `SettingsManager` paths infrastructure for cross-platform cache directories
   - TTL-based expiry (e.g. 7 days)
   - Max disk cache size configurable (default 100 MB)

### Loading Strategy

```
Request thumbnail for MediaItem
  → Check in-memory cache → HIT → return
  → Check disk cache → HIT → load into memory cache → return
  → Call provider.GetThumbnailAsync() → store in both caches → return
  → If provider returns null → use generic file-type icon fallback
```

---

## 10 – Integration Points with Existing Systems

### Primary Entry Point: DestinationSettingsView

The explorer is launched **exclusively from the Destination Settings config panel** for each configured provider instance. This is the natural location because:

1. Users are already here when configuring credentials — the button appears right after entering valid settings
2. Each instance has its own explorer context (e.g., different S3 buckets, different Imgur accounts)
3. The enabled/disabled state ties directly to the credential validation that already happens in `ValidateSettings()`

**Flow:**
```
User opens Destination Settings
  → Selects category (Image, Text, File)
  → Selects a configured instance (e.g. "Amazon S3 (Screenshots)")
  → Config panel shows: Identity, File Types, Provider Settings
  → IF provider implements IUploaderExplorer:
      → "Media Explorer" section appears at the bottom
      → IF ValidateSettings(settingsJson) returns true:
          → "Browse Files" button is ENABLED → click opens explorer window
      → ELSE:
          → Button is DISABLED + hint: "Configure credentials above to enable browsing."
```

### History Synergy

The existing `HistoryItem` (in [HistoryItem.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.History/HistoryItem.cs)) already stores:
- `URL`, `ThumbnailURL`, `DeletionURL`
- `Host` (provider name)
- `Tags` dictionary with `Favorite` support

**Future enhancement**: Cross-reference explorer items with local history to show upload date, original filename, and local file path alongside remote metadata.

### InstanceManager Integration

The [InstanceManager.cs](file:///home/xk/Documents/GitHub/XerahS/src/XerahS.Uploaders/PluginSystem/InstanceManager.cs) (23KB) manages configured uploader instances. The `UploaderInstanceViewModel` already has access to `ProviderCatalog.GetProvider()` in its constructor — we simply add the `IUploaderExplorer` check at the same point.

---

## 11 – plugin.json Schema Update

Add optional `supportsExplorer` field:

```json
{
  "pluginId": "amazons3",
  "name": "Amazon S3 Uploader",
  "version": "1.1.0",
  "supportsExplorer": true,
  "supportedCategories": ["Image", "Text", "File"],
  ...
}
```

This allows the catalog UI to badge plugins that support browsing without needing to load the assembly first.

---

## 12 – Documentation Plan

### Files to Create/Update

| File | Action | Content |
|------|--------|---------|
| `docs/development/explorer_plugin_guide.md` | **[NEW]** | Guide for 3rd-party developers implementing `IUploaderExplorer` |
| `docs/planning/plugin_implementation_plan.md` | **[UPDATE]** | Add section on explorer API |
| `CHANGELOG.md` | **[UPDATE]** | Document new explorer feature |

---

## 13 – Implementation Phases

### Phase 1: Core Contracts (smallest viable PR)

- [ ] Create `IUploaderExplorer.cs`, `MediaItem.cs`, `ExplorerQuery.cs`, `ExplorerPage.cs`
- [ ] Add `GetExplorer()` / `GetBrowsableProviders()` to `ProviderCatalog`
- [ ] Add `SupportsExplorer` to `PluginManifest`
- [ ] Unit test: verify `GetExplorer()` returns null for non-explorer providers

### Phase 2: S3 Explorer Plugin

- [ ] Implement `IUploaderExplorer` in `AmazonS3Provider`
- [ ] Integration test: list, get thumbnail, delete on a test bucket
- [ ] Requires valid S3 credentials in test config

### Phase 3: Explorer UI

- [ ] Add `SupportsExplorer`, `IsExplorerEnabled`, `OpenExplorerCommand` to `UploaderInstanceViewModel`
- [ ] Add "Media Explorer" section to `DestinationSettingsView.axaml` config panel
- [ ] Create `ProviderExplorerWindow.axaml` + `ProviderExplorerView.axaml` + `ProviderExplorerViewModel.cs`
- [ ] Test: button hidden for non-explorer providers (Paste2, Auto)
- [ ] Test: button disabled when credentials are missing, enabled when valid
- [ ] Test grid/list toggle, pagination, breadcrumb navigation
- [ ] Test thumbnail loading and caching

### Phase 4: Imgur Explorer + Polish

- [ ] Implement `IUploaderExplorer` in `ImgurProvider`
- [ ] Add search, sort, date range filtering
- [ ] Add download-to-local functionality
- [ ] Mobile-responsive layout testing (Android/iOS emulators)
- [ ] Documentation updates

---

## 14 – Verification Plan

### Automated Tests

```bash
# Run existing test suite to verify no regressions
dotnet test tests/
```

- **New unit tests**: `ProviderCatalog.GetExplorer()` returns correct values
- **New unit tests**: `MediaItem` serialization/deserialization
- **New unit tests**: `ExplorerQuery` defaults and validation

### Manual Verification

1. Build and run the app: `dotnet run --project src/XerahS.App`
2. Open Destination Settings, select a category (e.g., Image)
3. Select a non-explorer provider (e.g., Paste2) → verify no "Media Explorer" section visible
4. Select an S3 instance **without** credentials → verify "Browse Files" button is visible but **disabled** with hint text
5. Enter valid S3 credentials in Provider Settings → verify "Browse Files" button becomes **enabled**
6. Click "Browse Files" → verify explorer window opens with the instance name in title bar
7. Verify: folder navigation, breadcrumb clicks, back/forward
8. Verify: grid/list toggle, thumbnail loading, search filtering
9. Verify: copy URL, download, delete with confirmation

### Cross-Platform

- Test on Linux (primary dev platform), Windows, and macOS
- Mobile testing on Android emulator for responsive layout

---

## 15 – Summary of Deliverables

| Deliverable | Status |
|-------------|--------|
| `IUploaderExplorer` interface + models (`MediaItem`, `ExplorerQuery`, `ExplorerPage`) | To implement |
| `ProviderCatalog` updates (`GetExplorer()`, `GetBrowsableProviders()`) | To implement |
| `PluginManifest.SupportsExplorer` field | To implement |
| `DestinationSettingsView.axaml` "Browse Files" button + `UploaderInstanceViewModel` changes | To implement |
| `ProviderExplorerWindow.axaml` + `ProviderExplorerView.axaml` + `ProviderExplorerViewModel.cs` | To implement |
| Amazon S3 explorer implementation | To implement |
| Imgur explorer implementation | To implement |
| Thumbnail caching system | To implement |
| Plugin developer documentation | To write |

---

> [!NOTE]
> **Design Principles:**
> - Cursor-based pagination via `ExplorerQuery.ContinuationToken` (critical for S3 compatibility)
> - `IUploaderExplorer` as a separate opt-in interface to preserve binary compatibility with existing plugins
> - Per-instance explorer access from `DestinationSettingsView` with credential-gated enablement
> - `HistoryViewModel` as the reference pattern for the new explorer ViewModel
> - Two-level thumbnail caching (in-memory + disk) for performance
