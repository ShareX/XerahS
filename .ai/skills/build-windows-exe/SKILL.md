---
name: build-windows-exe
description: Build XerahS Windows executables (x64 or ARM64) with the packaging script. Use only for Windows packaging, Inno Setup, artifact checks, or packaging-specific lock recovery.
metadata:
  keywords:
    - build
    - windows
    - exe
    - installer
    - release
    - publish
    - packaging
    - file-lock
    - compilation
    - arm64
    - x64
    - inno-setup
---

You are an expert Windows build automation specialist for .NET projects.

Follow these instructions **exactly** and in order to build Windows executables for XerahS while avoiding common file locking issues.

<task>
  <goal>Build Windows installers for both x64 and ARM64 architectures.</goal>
  <goal>Handle file locking issues that commonly occur during compilation.</goal>
  <goal>Ensure Inno Setup successfully creates installer executables.</goal>
  <goal>Validate that the build artifacts exist and are recent.</goal>
</task>

<context>
  <build_script_path>build\windows\package-windows.ps1</build_script_path>
  <dist_output_path>dist</dist_output_path>
  <common_locked_file>ShareX.ImageEditor\src\ShareX.ImageEditor\obj\Release\net10.0-windows10.0.26100.0\ShareX.ImageEditor.dll</common_locked_file>
  <expected_outputs>
    - XerahS-{version}-win-x64.exe
    - XerahS-{version}-win-arm64.exe
  </expected_outputs>
</context>

## Shared Build Guardrails

Before Windows packaging work, follow [build-common](../build-common/SKILL.md) for shared timeout, lock recovery, no-concurrent-build, `-m:1`, TFM, and SkiaSharp rules. This Windows skill owns Windows packaging commands, Inno Setup behavior, and Windows artifact validation.

## Build Process

### Preparation

Build the pinned submodule revision. Update ImageEditor only when the task requests it, using the operator's Git wrapper.

Check for another build sharing these outputs. If locks occur, follow [build-common](../build-common/SKILL.md) to identify task-owned lock holders and clean only the affected outputs. Routine packaging does not require blanket process termination or cleanup.

### Phase 2: Initial Build Attempt

1. **Run the packaging script**:
   ```powershell
   .\build\windows\package-windows.ps1
   ```

2. **Check for successful completion**:
   - Script should complete with exit code 0
   - Both installers should be created in the `dist` folder

### Phase 3: Handle File Lock Failures

**If you see errors like**:
- `CS2012: Cannot open '...ShareX.ImageEditor.dll' for writing`
- `The process cannot access the file ... because it is being used by another process`
- `file may be locked by 'VBCSCompiler' or '.NET Host' or 'csc'`

**Then apply these fixes**:

1. Follow [build-common](../build-common/SKILL.md) to stop only verified task-owned lock holders.
2. Clean the affected project/output directory if the lock persists.

3. **Pre-build the `ShareX.ImageEditor` project separately with single-threaded compilation**:
   ```powershell
   dotnet build 'ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj' -c Release -p:UseSharedCompilation=false /m:1
   ```
   - The `/m:1` flag forces single-threaded build
   - `UseSharedCompilation=false` disables the VBCSCompiler server

4. **Re-run the packaging script**:
   ```powershell
   .\build\windows\package-windows.ps1
   ```

### Phase 4: Fallback - Manual ARM64 Build

**If ARM64 continues to fail but x64 succeeds**, build ARM64 manually:

1. Recover confirmed locks using [build-common](../build-common/SKILL.md).

2. **Pre-build `ShareX.ImageEditor`**:
   ```powershell
   dotnet build 'ShareX.ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj' -c Release -p:UseSharedCompilation=false /m:1
   ```

3. **Publish ARM64 manually**:
   ```powershell
   $root = (Get-Location).Path
   $project = "$root\src\desktop\app\XerahS.App\XerahS.App.csproj"
   $publishOutput = "$root\build\publish-temp-win-arm64"
   
   dotnet publish $project -c Release -p:OS=Windows_NT -r win-arm64 -p:PublishSingleFile=false -p:SkipBundlePlugins=true -p:UseSharedCompilation=false --self-contained true -o $publishOutput
   ```

4. **Publish plugins**:
   ```powershell
   $pluginsDir = "$publishOutput\Plugins"
   New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
   
   Get-ChildItem "$root\src\desktop\plugins" -Filter "*.csproj" -Recurse | ForEach-Object {
     $pluginId = $_.BaseName
     $pluginJsonPath = Join-Path $_.Directory.FullName "plugin.json"
     if (Test-Path $pluginJsonPath) {
       $json = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
       if ($json.pluginId) { $pluginId = $json.pluginId }
     }
     Write-Host "Publishing plugin: $pluginId"
     dotnet publish $_.FullName -c Release -r win-arm64 -p:UseSharedCompilation=false --self-contained false -o "$pluginsDir\$pluginId"
   }
   ```

5. **Run Inno Setup manually**:
   ```powershell
   $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
   $issScript = "$root\build\windows\XerahS-setup.iss"
   $version = ([xml](Get-Content "$root\Directory.Build.props")).SelectSingleNode("//Version").InnerText.Trim()
   $outputDir = "$root\dist"
   
   & $isccPath "/dMyAppReleaseDirectory=$publishOutput" "/dOutputBaseFilename=XerahS-$version-win-arm64" "/dOutputDir=$outputDir" $issScript
   ```

### Phase 5: Validation

1. **Check that both installers exist**:
   ```powershell
   Get-ChildItem 'dist' -Filter '*.exe' | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
   ```

2. **Verify they were created recently** (within the last few minutes)

3. **Expected output**:
   ```
   Name                          Length     LastWriteTime
   ----                          ------     -------------
   XerahS-{version}-win-arm64.exe  ~55-60MB   [Today's date]
   XerahS-{version}-win-x64.exe    ~55-60MB   [Today's date]
   ```

## Important Notes

### Locks and sequencing

Shared rules are in [build-common](../build-common/SKILL.md). Windows specifics: `ShareX.ImageEditor` multi-targets four TFMs that race on one DLL output, so `CS2012` is the usual symptom. `package-windows.ps1` already runs `win-x64` then `win-arm64` sequentially with `/m:1`; never run it twice at once. Other lock holders are VBCSCompiler, a running XerahS (the Watch Folder daemon loads Debug DLLs), and the Avalonia designer. A `CS2012` during a retry can still end in success, so check that `XerahS.exe` and the installers were actually created with fresh timestamps.

### Key Build Parameters
- `-p:UseSharedCompilation=false`: Disables VBCSCompiler server
- `-p:nodeReuse=false`: Prevents MSBuild from reusing build nodes
- `/m:1`: Forces single-threaded build (slower but no race conditions)
- `-p:SkipBundlePlugins=true`: Avoids custom MSBuild target path resolution bugs

## Success Criteria
- ✅ Both win-x64 and win-arm64 .exe installers created
- ✅ Files are ~55-60 MB in size
- ✅ Timestamps are recent (within build session)
- ✅ Dist folder contains the expected installer files

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "XerahS.exe" does not exist (Inno Setup) | The main app didn't publish; check for earlier build errors |
| CS2012 file lock error | Follow Phase 3 and [build-common](../build-common/SKILL.md) |
| Installer created but old timestamp | Build failed silently; check logs in `iscc_log_win-{arch}.txt` |
| Only x64 succeeds, ARM64 fails | Use Phase 4 manual ARM64 build process |
| All builds fail | Clean solution, restart terminal, ensure no XerahS instances running |

## Related Files
- Shared guardrails: [build-common](../build-common/SKILL.md)
- Build script: [build\windows\package-windows.ps1](../../../build/windows/package-windows.ps1)
- Inno Setup script: [build\windows\XerahS-setup.iss](../../../build/windows/XerahS-setup.iss)
- Version config: [Directory.Build.props](../../../Directory.Build.props)
- Build logs: `build/windows/iscc_log_win-{arch}.txt`
