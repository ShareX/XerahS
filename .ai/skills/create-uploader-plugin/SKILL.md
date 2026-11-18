---
name: create-uploader-plugin
description: Add a desktop upload destination under src/desktop/plugins. Do not use for configuring an existing plugin.
---

# Create Uploader Plugin

Use this skill to add a new desktop uploader plugin that matches the current XerahS plugin architecture.

For a concrete native-API example, see [docs/proposals/xip/XIP0048-nextcloud-native-plugin-design.md](../../../docs/proposals/xip/XIP0048-nextcloud-native-plugin-design.md).

## Workflow

1. Audit existing support before writing code.
   - Search the repo for the service name, old uploader code, prior docs, and compatibility layers.
   - Determine whether this should be a new plugin, a replacement for an old uploader, or an extension of an existing plugin.
2. Research the service's official API and auth model using primary sources.
   - Use [references/service-research-template.md](references/service-research-template.md).
   - Prefer the service's native API over generic compatibility endpoints when the native path materially improves auth, chunking, sharing, explorer actions, capability discovery, or reliability.
3. Inspect the nearest existing plugin before writing code.
   - Read [references/pattern-map.md](references/pattern-map.md).
   - Pick the closest starting point:
     - Simple token/manual config: `Bitly.Plugin`
     - Browser login or OAuth: `Dropbox.Plugin`
     - File storage + explorer: `AmazonS3.Plugin` or `Nextcloud.Plugin`
4. Scaffold the plugin with `scripts/new-uploader-plugin.ps1`.
5. Replace scaffold placeholders with service-specific logic.
6. Keep secrets in `ISecretStore`, not in settings JSON.
7. Add explorer support only if the destination can browse/list remote files with stable semantics.
8. Add or refine the Avalonia config UI when the property-grid experience would be weak.
   - For Avalonia control, binding, and styling rules, read [../avalonia-guidelines/SKILL.md](../avalonia-guidelines/SKILL.md).
   - For visual redesign of a plugin config view, read [../design-ui-window/SKILL.md](../design-ui-window/SKILL.md) and set its `target_view_path` to `src\desktop\plugins\<Name>.Plugin\Views\ConfigView.axaml`.
   - Treat uploader config UI as Avalonia AXAML work, not a web frontend workflow.
9. Build the new plugin project, then build `src/desktop/XerahS.sln`.

## Scaffold Command

Run:

```powershell
.ai\skills\create-uploader-plugin\scripts\new-uploader-plugin.ps1 `
  -PluginName "MyService" `
  -PluginId "myservice" `
  -DisplayName "MyService Uploader" `
  -AddToSolution
```

This creates:

- `src/desktop/plugins/MyService.Plugin/`
- `XerahS.MyService.Plugin.csproj`
- `plugin.json`
- config model, provider, uploader
- Avalonia config view + viewmodel

## Required Conventions

1. Keep the plugin under `src/desktop/plugins/<Name>.Plugin/`.
2. Target `net11.0`.
3. Reuse `src/desktop/plugins/Directory.Build.props`.
4. Use `ShareX.<Name>.Plugin` namespaces and `XerahS.<Name>.Plugin` assembly/project names.
5. Exclude Avalonia/runtime-shared dependencies from plugin output with `ExcludeAssets=runtime`.
6. Copy `plugin.json` to output.
7. If credentials are needed, store them through `ISecretStore`.
8. If importing legacy plaintext settings, implement `IInstanceSecretMigrator`.
9. Keep upload logic inside the plugin uploader/provider, not in core app code.

## Implementation Decisions

Use these rules when filling in the scaffold:

1. Use `UploaderProviderBase` unless there is a strong reason not to.
2. Return a concrete `Uploader` or `GenericUploader` from `CreateInstance`.
3. Override `CreateConfigView()` and `CreateConfigViewModel()` for non-trivial configuration.
4. Implement `IUploaderExplorer` only when the service supports remote listing/content actions.
5. Make `ValidateSettings` reflect real minimum runtime requirements.
6. Keep `plugin.json` metadata aligned with `ProviderId`, class namespace, and assembly name.
7. If the service exposes both generic and native endpoints, document why the chosen path is correct for this plugin.
8. If the service has staged login or capability discovery, surface that in the config UI instead of hiding it behind a flat settings form.
9. If the service already had legacy plaintext settings, add secret migration rather than silently abandoning stored credentials.

## Verification

Run both:

```powershell
dotnet build src\desktop\plugins\<Name>.Plugin\XerahS.<Name>.Plugin.csproj -m:1
dotnet build src\desktop\XerahS.sln -m:1
```

Use [references/review-checklist.md](references/review-checklist.md) before finishing.
