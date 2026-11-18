# Review Checklist

Before finishing a new plugin:

1. Existing repo support was audited before writing the new plugin.
2. Official API docs were reviewed, and the native-vs-compatibility choice is justified.
3. Folder path is `src/desktop/plugins/<Name>.Plugin/`.
4. `.csproj` targets `net11.0` and references `XerahS.Uploaders` and `XerahS.Common` with runtime assets excluded.
5. `plugin.json` `pluginId`, `entryPoint`, and `assemblyFileName` match the actual code.
6. Provider `ProviderId` matches `plugin.json` `pluginId`.
7. Config model serializes only non-secret settings.
8. Secrets are read and written via `ISecretStore`.
9. `ValidateSettings` matches real runtime requirements.
10. Config view and viewmodel keep bindings/API stable and compile cleanly.
11. If explorer support exists, `ListAsync`, `GetContentAsync`, `DeleteAsync`, and `CreateFolderAsync` have clear behavior.
12. If the plugin replaces or imports older settings, handle migration instead of leaving plaintext secrets behind.
13. Build the plugin project successfully.
14. Build `src/desktop/XerahS.sln` successfully.
