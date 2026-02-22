---
name: build-windows-exe
description: Builds Windows executables (x64 and ARM64) for XerahS using the packaging script. Handles common file locking issues during compilation by killing processes, clearing locks, and using single-threaded builds when needed. Ensures successful creation of Inno Setup installers.
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
  <build_script_path>ShareX Team\XerahS\build\windows\package-windows.ps1</build_script_path>
  <dist_output_path>ShareX Team\XerahS\dist</dist_output_path>
  <common_locked_file>ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\obj\Release\net10.0-windows10.0.26100.0\ShareX.ImageEditor.dll</common_locked_file>
  <expected_outputs>
    - XerahS-{version}-win-x64.exe
    - XerahS-{version}-win-arm64.exe
  </expected_outputs>
</context>

## Build Process

### Phase 0: Update ImageEditor Submodule

**Always pull the latest ImageEditor submodule before building** to ensure the embedded image editor is up-to-date.

```powershell
cd 'ShareX Team\XerahS'
git submodule update --remote --merge ImageEditor
```

### Phase 1: Pre-Build Cleanup

**CRITICAL**: File locking is the #1 cause of Windows build failures. Always clean before building.

1. **Kill all potential file-locking processes**:
   ```powershell
   Get-Process | Where-Object { 
     $_.Name -like '*XerahS*' -or 
     $_.Name -like '*dotnet*' -or 
     $_.Name -like '*MSBuild*' -or 
     $_.Name -like '*VBCSCompiler*' 
   } | Stop-Process -Force -ErrorAction SilentlyContinue
   ```

2. **Clean the solution**:
   ```powershell
   cd 'ShareX Team\XerahS'
   dotnet clean --nologo -c Release
   ```

3. **If file locks persist, delete the problematic obj folder**:
   ```powershell
   Remove-Item 'ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\obj' -Recurse -Force -ErrorAction SilentlyContinue
   ```

### Phase 2: Initial Build Attempt

1. **Run the packaging script**:
   ```powershell
   cd 'ShareX Team\XerahS\build\windows'
   .\package-windows.ps1
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

1. **Kill compiler processes again**:
   ```powershell
   Get-Process | Where-Object { 
     $_.Name -like '*VBCSCompiler*' -or 
     $_.Name -like '*dotnet*' -or 
     $_.Name -like '*csc*' 
   } | Stop-Process -Force -ErrorAction SilentlyContinue
   Start-Sleep -Seconds 2
   ```

2. **Remove the locked obj folder**:
   ```powershell
   Remove-Item 'ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\obj\Release' -Recurse -Force -ErrorAction SilentlyContinue
   ```

3. **Pre-build the ImageEditor project separately with single-threaded compilation**:
   ```powershell
   dotnet build 'ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj' -c Release -p:UseSharedCompilation=false /m:1
   ```
   - The `/m:1` flag forces single-threaded build
   - `UseSharedCompilation=false` disables the VBCSCompiler server

4. **Re-run the packaging script**:
   ```powershell
   cd 'ShareX Team\XerahS\build\windows'
   .\package-windows.ps1
   ```

### Phase 4: Fallback - Manual ARM64 Build

**If ARM64 continues to fail but x64 succeeds**, build ARM64 manually:

1. **Clean and kill processes**:
   ```powershell
   Get-Process | Where-Object { $_.Name -like '*VBCSCompiler*' -or $_.Name -like '*dotnet*' } | Stop-Process -Force -ErrorAction SilentlyContinue
   Remove-Item 'ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\obj\Release' -Recurse -Force -ErrorAction SilentlyContinue
   ```

2. **Pre-build ImageEditor**:
   ```powershell
   dotnet build 'ShareX Team\XerahS\ImageEditor\src\ShareX.ImageEditor\ShareX.ImageEditor.csproj' -c Release -p:UseSharedCompilation=false /m:1
   ```

3. **Publish ARM64 manually**:
   ```powershell
   $root = 'ShareX Team\XerahS'
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
   Get-ChildItem 'ShareX Team\XerahS\dist' -Filter '*.exe' | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
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

### Sequential Builds Are Mandatory

**NEVER run two builds at the same time.** ImageEditor targets multiple TFMs (`net9.0`, `net10.0`, `net9.0-windows10.0.26100.0`, `net10.0-windows10.0.26100.0`) and MSBuild parallelism causes all of them to race on the same `ShareX.ImageEditor.dll` output path, producing `CS2012` file lock errors.

- **Architectures**: `package-windows.ps1` already iterates `win-x64` then `win-arm64` sequentially via `foreach` — never invoke it twice concurrently.
- **Internal parallelism**: Controlled by `/m:1` on the `dotnet publish` call, which forces single-threaded MSBuild and eliminates the intra-build race.
- **Between builds**: Always run `dotnet build-server shutdown` and kill VBCSCompiler between consecutive build sessions.

### Why File Locking Happens
- **VBCSCompiler**: Roslyn compiler server caches assemblies for faster builds
- **Parallel builds**: Multiple projects trying to write the same DLL simultaneously
- **Running XerahS instances**: The Watch Folder daemon loads DLLs from Debug folder
- **Avalonia designer**: May hold references to compiled DLLs

### Key Build Parameters
- `-p:UseSharedCompilation=false`: Disables VBCSCompiler server
- `-p:nodeReuse=false`: Prevents MSBuild from reusing build nodes
- `/m:1`: Forces single-threaded build (slower but no race conditions)
- `-p:SkipBundlePlugins=true`: Avoids custom MSBuild target path resolution bugs

### Build Error Handling
- **Don't panic if you see CS2012 errors**: The build may still succeed
- **Always check if XerahS.exe was created**: The error might be during a retry
- **ImageEditor is the usual culprit**: It has parallel TFM builds (net9.0, net10.0, with/without Windows SDK)
- **ARM64 builds are more prone to locking**: They run after x64 which may leave processes

### Best Practices
1. **Always run Phase 1 cleanup first**
2. **Monitor the build output for "file may be locked by" messages**
3. **Check actual file creation, not just exit codes**
4. **Keep VBCSCompiler killed during builds**
5. **Close any running XerahS instances before building**

## Success Criteria
- ✅ Both win-x64 and win-arm64 .exe installers created
- ✅ Files are ~55-60 MB in size
- ✅ Timestamps are recent (within build session)
- ✅ No lingering build processes (VBCSCompiler, dotnet, MSBuild)
- ✅ Dist folder contains the expected installer files

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| "XerahS.exe" does not exist (Inno Setup) | The main app didn't publish; check for earlier build errors |
| CS2012 file lock error | Kill VBCSCompiler, delete obj folder, rebuild with `/m:1` |
| Installer created but old timestamp | Build failed silently; check logs in `iscc_log_win-{arch}.txt` |
| Only x64 succeeds, ARM64 fails | Use Phase 4 manual ARM64 build process |
| All builds fail | Clean solution, restart terminal, ensure no XerahS instances running |

## Related Files
- Build script: [build\windows\package-windows.ps1](../../../build/windows/package-windows.ps1)
- Inno Setup script: [build\windows\XerahS-setup.iss](../../../build/windows/XerahS-setup.iss)
- Version config: [Directory.Build.props](../../../Directory.Build.props)
- Build logs: `build/windows/iscc_log_win-{arch}.txt`
