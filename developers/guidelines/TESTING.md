# Testing & Verification

- **Requirement**: Suggest relevant tests if modifying executable code.
- **Conventions**: Align new tests with current conventions.
- **Historical Parity**:
  - Use `git show <hash>:<file>` to compare against historical ShareX behavior if needed.
  - Store references in `ref/` and clean up after validation.

## Mobile Theming Validation

- Run targeted mobile builds for UI and head projects when changing mobile styles:
  - `dotnet build src/XerahS.Mobile.UI/XerahS.Mobile.UI.csproj`
  - `dotnet build src/XerahS.Mobile.iOS.ShareExtension/XerahS.Mobile.iOS.ShareExtension.csproj`
  - `dotnet build src/XerahS.Mobile.iOS/XerahS.Mobile.iOS.csproj`
  - `dotnet build src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj`
- Capture manual simulator/device results in `docs/reports/`.
