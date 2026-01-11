# macOS Publish & Entitlements Checklist (osx-arm64)

1. Build/Publish
   - `dotnet publish -c Release -r osx-arm64 --self-contained true src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj`
   - Confirm output `.app` exists under `bin/Release/net10.0/osx-arm64/publish/`

2. Permissions
   - First launch: grant Screen Recording when prompted (needed for capture)
   - Grant Accessibility for hotkeys: `System Settings` → `Privacy & Security` → `Accessibility`

3. Runtime Smoke Test
   - Launch the `.app` bundle (not `dotnet run`)
   - Perform a region screenshot (should prompt Screen Recording if not granted)
   - Test global hotkey (SharpHook) once Accessibility is granted
   - Verify clipboard copy/paste (text + image) and file drop handling

4. Bundling/Metadata
   - Ensure `Info.plist` contains usage descriptions for Screen Recording/Accessibility
   - Verify app icon/identifier are set as expected in the bundle

5. Artifacts
   - Package `.app` or `.pkg` as needed for distribution
   - Keep `osx-arm64` artifact separate from `win-x64` builds with clear naming
