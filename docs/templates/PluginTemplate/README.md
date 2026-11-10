# XerahS Plugin Template

Copy this folder to `src/desktop/plugins/YourPluginName.Plugin/`. In the `.csproj`, replace the `ProjectReference` paths so they are relative to the plugin folder (e.g. when the plugin is at `src/desktop/plugins/MyService.Plugin/`, use `../../core/...`):

```xml
<ProjectReference Include="..\..\core\XerahS.UploaderPluginSdk\XerahS.UploaderPluginSdk.csproj" />
<ProjectReference Include="..\..\core\XerahS.Uploaders\XerahS.Uploaders.csproj" Private="false" ExcludeAssets="runtime" />
```

Then rename:

- **MyPlugin** → your plugin ID (e.g. `MyService`)
- **plugin.json** → set `pluginId`, `name`, `entryPoint` (full type name of your provider class)
- **.csproj** → set `PluginId`, `PluginName`, and project/assembly names
- **MyProvider.cs** → your provider class name; implement `IUploaderProvider` (or inherit `UploaderProviderBase` from XerahS.Uploaders)
- **MyConfigModel.cs** → your settings POCO

Then add your plugin project to the solution and to the app's plugin discovery (or use the existing `plugins\*\*.csproj` glob if under `plugins/`).

Requirements:

- Reference **XerahS.UploaderPluginSdk** (contracts). Optionally reference **XerahS.Uploaders** for `UploaderProviderBase`, `Uploader`, `GenericUploader`.
- Provide a type that implements `IUploaderProvider` and expose its full name in `plugin.json` → `entryPoint`.
- Build as a library; the host loads the assembly and instantiates the entry point type.

See [PLUGIN_SDK.md](../../../developers/guidelines/PLUGIN_SDK.md) for full details.
