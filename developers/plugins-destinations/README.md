# Destination Plugin Development Guide

This is the consolidated reference for creating new destination plugins in XerahS.

It merges the practical parts of:
- `developers/plugins-destinations/README.md`
- `developers/plugins-destinations/exporter.md`
- `developers/plugins-destinations/implementation_plan.md`
- `developers/plugins-destinations/packaging_system.md`

and aligns them with the current code contracts under `src/XerahS.Uploaders/PluginSystem`.

## 1. What Makes "Browse Files" Work

`Destination Settings -> Browse Files` (Media Explorer) only works when all of these are true:

1. Your provider implements `IUploaderExplorer`.
2. `ValidateSettings(settingsJson)` returns `true` for the selected instance.
3. `ListAsync` returns `MediaItem` entries.
4. `GetThumbnailAsync` returns image bytes for image items (otherwise you only see file icons, not thumbnails).

Code path:
- UI checks `provider is IUploaderExplorer`.
- Button enabled state depends on `ValidateSettings`.
- Explorer window calls:
  - `ListAsync`
  - `GetThumbnailAsync`
  - `GetContentAsync`
  - `DeleteAsync`

Practical guidance:
- Override `ValidateSettings` if your provider needs secrets or required fields. The base implementation only checks JSON deserialization.
- In each `MediaItem`, set `Name`, `Path`, `MimeType`, and `Url` when possible.
- Use `MediaItem.Metadata` for provider-specific context required by later calls (`DeleteAsync`, `GetContentAsync`, etc.).

## 2. Project Setup

Create a class library plugin project (usually `net10.0`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <PluginId>myplugin</PluginId>
    <PluginName>My Destination</PluginName>
  </PropertyGroup>

  <ItemGroup>
    <!-- Match host package versions used by the app -->
    <PackageReference Include="Avalonia" Version="11.3.12">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.12">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\..\\XerahS.Uploaders\\XerahS.Uploaders.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
    <ProjectReference Include="..\\..\\XerahS.Common\\XerahS.Common.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>

  <ItemGroup>
    <None Include="plugin.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

Why `ExcludeAssets=runtime` matters:
- If you copy framework/shared DLLs into plugin folders, config view loading can break due to assembly identity conflicts.
- Keep plugin output minimal: plugin DLL, `plugin.json`, and only true plugin-specific dependencies.

## 3. Manifest (`plugin.json`)

Minimum valid manifest:

```json
{
  "pluginId": "myplugin",
  "name": "My Destination",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Uploads files to My Service",
  "apiVersion": "1.0",
  "entryPoint": "MyPlugin.MyProvider",
  "assemblyFileName": "MyPlugin.dll",
  "supportedCategories": ["Image", "File"]
}
```

Important fields:
- `pluginId`: must be unique; should match `ProviderId`.
- `apiVersion`: must be compatible with current plugin API (`1.0`).
- `entryPoint`: full type name implementing `IUploaderProvider`.
- `assemblyFileName`: optional but recommended.
- `supportedCategories`: at least one category required.
- `supportsExplorer`: optional metadata flag; set `true` when provider implements `IUploaderExplorer`.

## 4. Provider Implementation (`IUploaderProvider`)

Use `UploaderProviderBase` unless you need full custom behavior:

```csharp
using Newtonsoft.Json;
using XerahS.Uploaders;
using XerahS.Uploaders.FileUploaders;
using XerahS.Uploaders.PluginSystem;

namespace MyPlugin;

public sealed class MyProvider : UploaderProviderBase
{
    public override string ProviderId => "myplugin";
    public override string Name => "My Destination";
    public override string Description => "Uploads files to My Service";
    public override Version Version => new(1, 0, 0);
    public override UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image, UploaderCategory.File };
    public override Type ConfigModelType => typeof(MyConfigModel);

    public override Uploader CreateInstance(string settingsJson)
    {
        var config = JsonConvert.DeserializeObject<MyConfigModel>(settingsJson)
            ?? throw new InvalidOperationException("Invalid settings JSON");

        return new MyUploader(config);
    }

    public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() =>
        new()
        {
            [UploaderCategory.Image] = new[] { "png", "jpg", "jpeg", "gif", "webp" },
            [UploaderCategory.File] = new[] { "zip", "pdf", "txt" }
        };

    public override object? CreateConfigView() => new Views.MyConfigView();
    public override IUploaderConfigViewModel? CreateConfigViewModel() => new ViewModels.MyConfigViewModel();

    public override bool ValidateSettings(string settingsJson)
    {
        var cfg = JsonConvert.DeserializeObject<MyConfigModel>(settingsJson);
        return cfg != null && !string.IsNullOrWhiteSpace(cfg.ApiBaseUrl);
    }
}
```

## 5. Config UI and Secret Handling

Config view model contract:
- Implement `IUploaderConfigViewModel`:
  - `LoadFromJson`
  - `ToJson`
  - `Validate`

Secrets:
- Do not store secrets directly in `settingsJson`.
- Store a generated `SecretKey` in settings.
- Save actual credentials in `ISecretStore` (`GetSecret`, `SetSecret`, `DeleteSecret`).
- If your provider or config VM needs host services, implement `IProviderContextAware`.

## 6. Optional: Media Explorer Support (`IUploaderExplorer`)

Implement `IUploaderExplorer` to enable `Browse Files`.

Required methods:
- `ListAsync(ExplorerQuery query, ...)`
- `GetThumbnailAsync(MediaItem item, ...)`
- `GetContentAsync(MediaItem item, ...)`
- `DeleteAsync(MediaItem item, ...)`
- `CreateFolderAsync(...)`

Skeleton:

```csharp
using XerahS.Uploaders.PluginSystem;

public sealed class MyProvider : UploaderProviderBase, IUploaderExplorer
{
    public bool SupportsFolders => true;

    public Task<ExplorerPage> ListAsync(ExplorerQuery query, CancellationToken cancellation = default)
    {
        // query.SettingsJson has the instance config for each call.
        // Return files/folders as MediaItem entries.
        throw new NotImplementedException();
    }

    public Task<byte[]?> GetThumbnailAsync(MediaItem item, int maxWidthPx = 180, CancellationToken cancellation = default)
    {
        // Return JPEG/PNG bytes for image items.
        throw new NotImplementedException();
    }

    public Task<Stream?> GetContentAsync(MediaItem item, CancellationToken cancellation = default)
        => Task.FromResult<Stream?>(null);

    public Task<bool> DeleteAsync(MediaItem item, CancellationToken cancellation = default)
        => Task.FromResult(false);

    public Task<bool> CreateFolderAsync(string parentPath, string folderName, CancellationToken cancellation = default)
        => Task.FromResult(false);
}
```

Explorer-specific data tips:
- `MediaItem.Url`: used for open/copy URL actions.
- `MediaItem.ThumbnailUrl`: can be used by your own `GetThumbnailAsync` logic.
- `MediaItem.Metadata`: include IDs, paths, serialized settings, etc. needed by follow-up calls.
- Respect paging: set `ExplorerPage.ContinuationToken` when more data exists.

## 7. Build, Deploy, and Test

Manual local deployment:

1. Build plugin project.
2. Create plugin folder:
   - `<AppOutput>/Plugins/<pluginId>/`
3. Copy:
   - plugin DLL
   - `plugin.json`
   - plugin-only dependencies
4. Start XerahS, open Destination Settings, and add plugin from catalog.

Quick validation checklist:
- Provider loads and appears in catalog.
- Config view renders.
- Settings round-trip (`LoadFromJson` / `ToJson`) works.
- Upload works for each declared category.
- If explorer implemented:
  - Browse Files button visible.
  - Browse Files button enabled with valid settings.
  - `ListAsync` returns items.
  - image thumbnails appear from `GetThumbnailAsync`.

## 8. Packaging Notes

Packaging support exists via:
- `src/XerahS.Uploaders/PluginSystem/PluginPackager.cs`
- `src/XerahS.PluginExporter/Program.cs`

Current behavior in code:
- Packages are ZIP-based.
- Max package size is 100 MB.
- CLI currently resolves `.xsdp` output by default.

Installer UI currently filters for `.xsdp` files. Ensure your packaging workflow uses this extension.

## 9. Reference Implementations

Use these plugins as examples:
- `src/Plugins/ShareX.AmazonS3.Plugin` (uploader + explorer + secure credential flow)
- `src/Plugins/ShareX.Imgur.Plugin` (uploader + explorer with album hierarchy)
- `src/Plugins/ShareX.GitHubGist.Plugin` (text uploader with OAuth token storage)
- `src/Plugins/ShareX.Paste2.Plugin` (simple text uploader)

