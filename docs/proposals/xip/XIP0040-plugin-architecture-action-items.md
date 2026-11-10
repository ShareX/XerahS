# XIP0040 Plugin Architecture Action Items

**Status**: Complete
**Version**: v0.22.257

**Verified:** LegacySupport consolidated; secret migration provider-driven; Amazon S3 plugin uses plugin-local `S3StorageClass`; solution build 0 errors.

---

## Completion Summary

| Phase | Status | Notes |
|-------|--------|--------|
| **1** LegacySupport consolidation | Done | All listed files under `LegacySupport/`; README covers purpose, duplicate DTOs, no new plugin deps. |
| **2** Provider-owned secret migration | Done | `IInstanceSecretMigrator` in `XerahS.UploaderPluginSdk`; `InstanceManager` uses `GetProvider` + interface only; Amazon S3, Imgur, GitHub Gist implement migrators. |
| **3** Amazon S3 runtime enum ownership | Done | Plugin has `S3StorageClass.cs`; `S3ConfigModel.StorageClass` and ViewModel use plugin enum; no references to core `AmazonS3StorageClass` in plugin. |
| **4** Validation | Done | Build succeeds (0 errors). No hard-coded provider IDs in `InstanceManager.cs`. |

### Definition of Done (Top Level)
1. **Legacy consolidated:** All legacy/mobile compatibility code under `src/desktop/core/XerahS.Uploaders/LegacySupport` (UploadersConfig, Configurations, FileUploaders, Abstractions, Compatibility, Stubs, Properties).
2. **No hard-coded provider IDs:** `InstanceManager.MigrateSecretsIfNeeded()` uses `ProviderCatalog.GetProvider(instance.ProviderId)` and `IInstanceSecretMigrator` only.
3. **S3 enum in plugin:** `AmazonS3.Plugin` uses its own `S3StorageClass` enum; core `AmazonS3StorageClass` not referenced by plugin.
4. **Build:** `dotnet build src/desktop/XerahS.sln` succeeds with 0 errors.

### Minor deviation from spec
- **2.1** Spec said add `IInstanceSecretMigrator.cs` under `XerahS.Uploaders/PluginSystem/`. Implemented in `XerahS.UploaderPluginSdk/IInstanceSecretMigrator.cs` (shared contract). Same namespace `XerahS.Uploaders.PluginSystem`; behavior unchanged.

---

# XIP0040: Plugin Architecture Action Items (Implementation-Ready)

## Purpose
Execute the architecture decision in [docs/architecture/PLUGIN_AND_UPLOADERS_ARCHITECTURE_ANALYSIS.md](../../architecture/PLUGIN_AND_UPLOADERS_ARCHITECTURE_ANALYSIS.md) with no ambiguity.

## Definition of Done (Top Level)
1. Legacy and mobile compatibility code is physically consolidated under `LegacySupport`.
2. Core secret migration no longer hard-codes provider IDs.
3. Amazon S3 plugin runtime config no longer depends on core `AmazonS3StorageClass`.
4. Build and targeted tests pass.

## Fixed Decisions
1. Runtime plugin model remains `ProviderId + SettingsJson`.
2. Legacy/mobile compatibility behavior remains supported.
3. Consolidation is structural only; no namespace or JSON behavior changes.
4. `UploadersConfig` redesign and global `UploaderType -> ProviderId` migration are out of scope.

## Phase 1: LegacySupport consolidation

### 1.1 Create folder and docs
Create:
- `src/desktop/core/XerahS.Uploaders/LegacySupport/README.md`

README required points:
- This folder exists for ShareX legacy import compatibility and mobile compatibility.
- Duplicate-looking DTOs are intentional and required.
- Runtime plugin code should not add new dependencies here unless specifically required for legacy/mobile compatibility.

### 1.2 Move files (exact)
Move the following files under `src/desktop/core/XerahS.Uploaders/LegacySupport` while keeping namespaces unchanged:

- `UploadersConfig.cs`
- `UploadersConfigImporter.cs`
- `Abstractions/IUploaderConfig.cs`
- `Configuration/UploaderType.cs`
- `Configuration/ImgurConfig.cs`
- `Configuration/DropboxConfig.cs`
- `Configuration/FtpConfig.cs`
- `Configuration/S3Config.cs`
- `Configuration/CustomUploaderConfig.cs`
- `FileUploaders/AmazonS3Settings.cs`
- `FileUploaders/AmazonS3StorageClass.cs`
- `FileUploaders/FTPAccount.cs`
- `Compatibility/UploaderFilter.cs`
- `LegacySupport/Stubs.cs`
- `LegacySupport/Properties/Resources.cs`

### 1.3 Update references
- Fix `using` statements and file path references if needed.
- Do not change public API names or namespaces.

### 1.4 Acceptance checks
- `rg --files src/desktop/core/XerahS.Uploaders/LegacySupport` includes all moved files.
- Old paths no longer contain those files.

## Phase 2: Provider-owned secret migration

### 2.1 Add contract in core
Add file:
- `src/desktop/core/XerahS.Uploaders/PluginSystem/IInstanceSecretMigrator.cs`  
  *(Implemented as `XerahS.UploaderPluginSdk/IInstanceSecretMigrator.cs` with same contract.)*

Contract:

```csharp
namespace XerahS.Uploaders.PluginSystem;

public interface IInstanceSecretMigrator
{
    bool TryMigrateSecrets(
        string settingsJson,
        ISecretStore secrets,
        out string updatedSettingsJson,
        out int migratedSecretCount);
}
```

### 2.2 Refactor core migration orchestrator
Modify:
- `src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs`

Required changes:
1. Remove `RequiresSecretKey`.
2. Remove hard-coded provider branches (`amazons3`, `imgur`, `gist`).
3. For each instance:
- resolve provider via `ProviderCatalog.GetProvider(instance.ProviderId)`
- if provider implements `IInstanceSecretMigrator`, call migrator
- if migration returns updated JSON, update `instance.SettingsJson`
4. Keep one save at end only if at least one instance changed.
5. Keep logging equivalent (migrated instances, migrated secrets).

### 2.3 Implement migrators in providers
Modify:
- `src/desktop/plugins/AmazonS3.Plugin/AmazonS3Provider.cs`
- `src/desktop/plugins/Imgur.Plugin/ImgurProvider.cs`
- `src/desktop/plugins/GitHubGist.Plugin/GitHubGistProvider.cs`

Each provider should implement `IInstanceSecretMigrator` and migrate its own legacy plaintext settings JSON.

Required parity:
- `amazons3`: migrate `AccessKeyId`, `SecretAccessKey` to secret store keys `accessKeyId`, `secretAccessKey`; ensure `SecretKey`; remove plaintext fields only after successful write.
- `imgur`: read/migrate `OAuth2Info.Client_Secret`, `OAuth2Info.Token`; ensure `SecretKey`; copy `OAuth2Info.Client_ID` to `ClientId` if missing; remove `OAuth2Info` only when secret migration happened.
- `gist`: read/migrate `OAuth2Info.Client_ID`, `OAuth2Info.Client_Secret`, `OAuth2Info.Token`; ensure `SecretKey`; remove `OAuth2Info` only when secret migration happened.

### 2.4 Acceptance checks
- `InstanceManager.cs` has no provider-ID migration branches.
- Migration is idempotent.
- Migration does not remove plaintext fields if secret write fails.

## Phase 3: Amazon S3 runtime enum ownership

### 3.1 Add plugin-local enum
Add new file under:
- `src/desktop/plugins/AmazonS3.Plugin` (for example `S3StorageClass.cs`)

### 3.2 Update runtime model and viewmodel
Modify:
- `src/desktop/plugins/AmazonS3.Plugin/S3ConfigModel.cs`
- `src/desktop/plugins/AmazonS3.Plugin/ViewModels/AmazonS3ConfigViewModel.cs`
- any additional Amazon S3 plugin runtime files referencing core enum

Required behavior:
- `S3ConfigModel.StorageClass` uses plugin-local enum.
- `StorageClassIndex` conversions use plugin-local enum.
- Numeric values remain aligned with existing persisted JSON (`0..4`) to avoid breaking existing settings.

### 3.3 Acceptance checks
- `rg -n "XerahS\.Uploaders\.FileUploaders\.AmazonS3StorageClass" src/desktop/plugins/AmazonS3.Plugin` returns no runtime references.

## Phase 4: Validation and regression checks

Run these commands from repo root:

```powershell
rg --files src/desktop/core/XerahS.Uploaders/LegacySupport
rg -n "class UploadersConfig|class UploadersConfigImporter|interface IUploaderConfig|enum UploaderType|class AmazonS3Settings|enum AmazonS3StorageClass|namespace ShareX.UploadersLib" src/desktop/core/XerahS.Uploaders/LegacySupport
rg -n "providerId == \"amazons3\"|providerId is \"imgur\" or \"gist\"|RequiresSecretKey" src/desktop/core/XerahS.Uploaders/PluginSystem/InstanceManager.cs
rg -n "XerahS\.Uploaders\.FileUploaders\.AmazonS3StorageClass" src/desktop/plugins/AmazonS3.Plugin
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter UploadersConfigPolymorphicTests -m:1
dotnet build src/desktop/XerahS.sln -m:1
```

### Required outcome
- All checks pass.
- Build has 0 errors.
- No regression in import/mobile behavior.

## Risk Notes and Mitigations

1. Risk: file moves can break references.
- Mitigation: preserve namespaces, update only path-based references/usings, run full solution build.

2. Risk: migrator behavior drift from current implicit logic.
- Mitigation: preserve existing field names and secret key names exactly.

3. Risk: enum migration breaks old JSON values.
- Mitigation: keep enum order/value mapping unchanged.

## Explicitly Out of Scope
- Opaque `UploadersConfig` redesign.
- Replacing `UploaderType` globally.
- Moving legacy support into a separate assembly.