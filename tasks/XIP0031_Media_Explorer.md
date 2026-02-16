# XIP0031 Media Explorer

**Status**: Draft  
**Created**: 2026-02-17  
**Updated**: 2026-02-17  
**Area**: Uploaders  
**Goal**: Add a provider-backed Media Explorer to browse, preview, and manage remote files.

---

## Overview

XerahS has a flexible uploader plugin system but no way to browse content already stored with a provider. Users must leave the app to view or manage items on S3, Imgur, or other services. This XIP introduces a Media Explorer that is backed by provider capabilities and launched per configured uploader instance.

The design reuses existing infrastructure: the plugin catalog for discovery, `HistoryViewModel` patterns for pagination and thumbnails, and `TaskManager` for upload flows. Explorer support is optional and opt-in via a new interface so existing plugins remain binary compatible.

Key principles: keep provider logic in the uploader layer, keep UI in Avalonia, and avoid any new upload pipeline or settings cloning logic. Providers that cannot browse simply do not implement the new interface.

---

## Prerequisites

- .NET 10 SDK and the target framework `net10.0-windows10.0.26100.0`
- Valid credentials for at least one provider (Amazon S3 for Phase 2, Imgur for Phase 4)

---

## Implementation Phases

### Phase 1: Core Contracts

Define the explorer interface and models in the uploader plugin system, plus catalog helpers to discover explorer-capable providers.

**Key Files:**
- `src/XerahS.Uploaders/PluginSystem/IUploaderExplorer.cs`
- `src/XerahS.Uploaders/PluginSystem/MediaItem.cs`
- `src/XerahS.Uploaders/PluginSystem/ExplorerQuery.cs`
- `src/XerahS.Uploaders/PluginSystem/ExplorerPage.cs`
- `src/XerahS.Uploaders/PluginSystem/ProviderCatalog.cs`
- `src/XerahS.Uploaders/PluginSystem/PluginManifest.cs`

**Code Example:**
```csharp
public interface IUploaderExplorer
{
    bool SupportsFolders { get; }

    Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default);
    Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default);
    Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default);
    Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default);
    Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default);
}
```

**Rules:**
- Keep `IUploaderProvider` unchanged to preserve binary compatibility.
- Use `ExplorerQuery.ContinuationToken` to support cursor-based pagination.

### Phase 2: Amazon S3 Explorer

Implement `IUploaderExplorer` for Amazon S3 using `ListObjectsV2`, `GetObject`, and `DeleteObject`. Support folder-like navigation via `Prefix` and `Delimiter`.

**Key Files:**
- `src/Plugins/ShareX.AmazonS3.Plugin/AmazonS3Provider.cs`

**Rules:**
- Reuse existing S3 signing and request utilities.
- Do not introduce a parallel upload pipeline for S3.

### Phase 3: Explorer UI

Add a per-instance "Browse Files" entry point in Destination Settings and implement the explorer window and view model using patterns from `HistoryViewModel`.

**Key Files:**
- `src/XerahS.UI/ViewModels/UploaderInstanceViewModel.cs`
- `src/XerahS.UI/Views/DestinationSettingsView.axaml`
- `src/XerahS.UI/ViewModels/ProviderExplorerViewModel.cs`
- `src/XerahS.UI/Views/ProviderExplorerView.axaml`
- `src/XerahS.UI/Views/ProviderExplorerWindow.axaml`

**Rules:**
- Bind each explorer window to a specific `UploaderInstance`; do not add a provider selector to the explorer UI.
- Thumbnails follow the `HistoryViewModel` background loading pattern.

### Phase 4: Imgur Explorer and Polish

Implement an Imgur explorer using the existing album APIs. Add search, sort, and download workflows, and refine responsive layout for mobile.

**Key Files:**
- `src/Plugins/ShareX.Imgur.Plugin/ImgurProvider.cs`
- `src/XerahS.UI/ViewModels/ProviderExplorerViewModel.cs`

**Rules:**
- Prefer Imgur thumbnail URLs when available.
- Keep UI filtering logic in the view model; providers only return raw lists.

---

## Non-Negotiable Rules

1. Do not create a new upload pipeline; all uploads remain in `TaskManager`.
2. Core services must not depend on Avalonia or platform-specific code.
3. Platform heads must not call providers directly; only via core or shared services.
4. Reuse existing settings cloning and validation (`TaskSettings.Clone`, `ValidateSettings`).
5. No UI in core libraries; UI remains in `XerahS.UI`.

---

## Deliverables

1. `IUploaderExplorer` interface and explorer models.
2. Provider catalog helpers for explorer discovery.
3. Amazon S3 explorer implementation.
4. Explorer window and view model with thumbnails, search, and pagination.
5. Imgur explorer implementation and UI polish.
6. Documentation for third-party plugin developers.

---

## Affected Components

- XerahS.Uploaders: `IUploaderExplorer`, `ExplorerQuery`, `ExplorerPage`, `MediaItem`, `ProviderCatalog`, `PluginManifest`
- XerahS.UI: `UploaderInstanceViewModel`, `DestinationSettingsView`, `ProviderExplorerViewModel`, `ProviderExplorerView`, `ProviderExplorerWindow`
- Plugins: `ShareX.AmazonS3.Plugin`, `ShareX.Imgur.Plugin`
- Docs: `docs/development/explorer_plugin_guide.md`, `docs/planning/plugin_implementation_plan.md`, `docs/CHANGELOG.md`

---

## Architecture Summary

```
UploaderInstance
    ↓
ProviderExplorerViewModel  ←  IUploaderExplorer (optional)
    ↓
ProviderCatalog → Provider Plugin
    ↓
Remote Storage API (S3, Imgur, ...)
```

---

## Evolution History

| Date | Change | Rationale |
|------|--------|-----------|
| 2026-02-17 | Converted Media Explorer Rev0 plan into XIP0031 | Align with XIP format and tracking |
