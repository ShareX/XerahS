# XerahS Uploader Plugin SDK – Developer Guide

This guide explains how to build an **uploader destination plugin** for XerahS using the **XerahS.UploaderPluginSdk** package and optional **XerahS.Uploaders** for base classes.

## Overview

- **XerahS.UploaderPluginSdk** (NuGet or project reference): interfaces and DTOs only — `IUploaderProvider`, `PluginManifest`, `UploaderInstance`, `UploaderCategory`, etc. No implementation.
- **XerahS.Uploaders** (optional): base classes and runtime types — `UploaderProviderBase`, `Uploader`, `GenericUploader`, etc. Plugins that run inside the desktop app typically reference both.

Your plugin is a .NET class library. The host discovers plugins (e.g. by scanning `Plugins/*/` or a list of projects), loads the assembly, reads `plugin.json`, and instantiates the type specified in `entryPoint` (must implement `IUploaderProvider`).

## 1. Project setup

- Target **net11.0** (or the same TFM as the host).
- Add a reference to **XerahS.UploaderPluginSdk**.
- Optionally add a reference to **XerahS.Uploaders** (with `Private=false`, `ExcludeAssets=runtime` if you want to use the host's copy at runtime).
- Set `CopyLocalLockFileAssemblies` and `EnableDynamicLoading` if the host loads the plugin from a separate folder.

Example (when the plugin is under `src/desktop/plugins/MyName.Plugin/`):

```xml
<ProjectReference Include="..\..\core\XerahS.UploaderPluginSdk\XerahS.UploaderPluginSdk.csproj" />
<ProjectReference Include="..\..\core\XerahS.Uploaders\XerahS.Uploaders.csproj" Private="false" ExcludeAssets="runtime" />
```

## 2. plugin.json

Create a `plugin.json` next to your plugin DLL (and include it in the project with `CopyToOutputDirectory="PreserveNewest"`). Required fields:

| Field | Description |
|-------|-------------|
| `pluginId` | Unique ID (e.g. `"imgur"`, `"paste2"`). |
| `name` | Display name. |
| `apiVersion` | Must match host API (e.g. `"1.0"`). |
| `entryPoint` | Full .NET type name of the class implementing `IUploaderProvider` (e.g. `"ShareX.Paste2.Plugin.Paste2Provider"`). |
| `supportedCategories` | Array of categories: `Image`, `Text`, `File`, `UrlShortener`, `UrlSharing`. |

Optional: `version`, `author`, `description`, `assemblyFileName`, `configViewId`, `dependencies`, `homepageUrl`, `supportsExplorer`, `supportsCancellation`, `supportsProgress`, `supportsResume`. These capability flags are additive and do not require an `apiVersion` major bump.

Example:

```json
{
  "pluginId": "myservice",
  "name": "My Service",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "entryPoint": "MyPlugin.Provider.MyProvider",
  "assemblyFileName": "MyPlugin.Plugin.dll",
  "supportedCategories": ["Image"],
  "supportsExplorer": false
}
```

## 3. Implement IUploaderProvider

Implement the interface (or inherit **UploaderProviderBase** from XerahS.Uploaders):

- **ProviderId**, **Name**, **Description**, **Version**, **SupportedCategories**, **ConfigModelType**.
- **CreateInstance(string settingsJson)** — return an object the host can upload with. Preferred: implement **IUploadHandler** (`UploadAsync(UploadRequest, CancellationToken)` → **UploadOutcome**). Legacy: inherit **GenericUploader**; the host adapts `Upload(Stream, fileName)` on a worker thread. Do not take a dependency on `HttpWebRequest` for new plugins; use `request.Host.CreateHttpClient(infiniteTimeout: true)` or `XerahS.Common.HttpClientFactory`.
- **GetSupportedFileTypes()** — which file extensions (e.g. image types) each category supports.
- **ValidateSettings**, **GetDefaultSettings** — validation and default JSON for new instances.
- **CreateConfigView()** / **CreateConfigViewModel()** — optional Avalonia UI. Prefer **GetConfigSchema()** so the host renders settings without loading plugin UserControls.
- **Capabilities** — optional flags (cancellation, progress, explorer, resume).
- **ConfigChanged** — optional event when provider config changes.

If you use **UploaderProviderBase**, you only override **CreateInstance** (returning **Uploader**) and the abstract members; the base implements **IUploaderProvider** (including **CreateInstance** as **object** via explicit interface implementation).

## 4. Optional: IUploaderExplorer

If your destination supports browsing (Media Explorer), implement **IUploaderExplorer** on the same class (or a type the host can resolve):

- **SupportsFolders**, **ListAsync**, **GetThumbnailAsync**, **GetContentAsync**, **DeleteAsync**, **CreateFolderAsync**.

Use **ExplorerQuery**, **ExplorerPage**, **MediaItem** from the SDK.

## 5. Build and deploy

- Build the plugin as a library. The host expects the DLL and `plugin.json` in a folder (e.g. `Plugins/<PluginId>/`). Publish or copy output accordingly (see `Directory.Build.props` in `src/desktop/plugins/` for how existing plugins set `PluginPublishBaseDir` and `CopyToPluginsDir`).

## 6. Template

A minimal copyable template is in **docs/templates/PluginTemplate/** (README, .csproj, plugin.json, a minimal provider and config model). Copy it to `src/desktop/plugins/YourName.Plugin/`, fix project reference paths (use `../../core/...` relative to the plugin folder), and rename types/IDs as needed.

## References

- **XerahS.UploaderPluginSdk** package README (or repo `src/desktop/core/XerahS.UploaderPluginSdk/README.md`) for the full contract list.
- **docs/architecture/PLUGIN_AND_UPLOADERS_ARCHITECTURE_ANALYSIS.md** for architecture context.
- **docs/proposals/xip/XIP0040-plugin-architecture-action-items.md** for the plugin/SDK roadmap.
