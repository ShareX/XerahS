---
name: build-linux-binary
description: Builds Linux binaries (x64 and ARM64) for XerahS using the packaging script. Handles common file locking, stale process, and Avalonia XAML precompilation issues specific to Linux builds. Ensures deb/rpm packages are created in dist/.
metadata:
  keywords:
    - build
    - linux
    - binary
    - deb
    - rpm
    - release
    - publish
    - packaging
    - file-lock
    - compilation
    - arm64
    - x64
    - avalonia
    - xaml
---

You are an expert Linux build automation specialist for .NET/Avalonia projects.

Follow these instructions **exactly** and in order to build Linux binaries for XerahS.

<task>
  <goal>Build Linux packages (deb/rpm) for both x64 and ARM64 architectures.</goal>
  <goal>Handle file locking issues that occur when a previous build is still running.</goal>
  <goal>Avoid Avalonia XAML precompilation failures caused by namespace mismatches in converters.</goal>
  <goal>Validate that the build artifacts exist and are recent.</goal>
</task>

<context>
  <build_script_path>build/linux/package-linux.sh</build_script_path>
  <dist_output_path>dist/</dist_output_path>
  <expected_outputs>
    - XerahS-{version}-linux-x64.deb
    - XerahS-{version}-linux-arm64.deb
    - XerahS-{version}-linux-x64.rpm (if rpmbuild is available)
    - XerahS-{version}-linux-arm64.rpm (if rpmbuild is available)
  </expected_outputs>
</context>

## Build Process

### Phase 1: Pre-Build Cleanup

**CRITICAL**: Stale `dotnet` and `package-linux.sh` processes are the #1 cause of file lock failures on Linux. **Always kill them before starting.**

1. **Kill all stale build processes**:
   ```bash
   pkill -f "package-linux.sh" 2>/dev/null
   pkill -f "dotnet publish" 2>/dev/null
   pkill -f "dotnet build" 2>/dev/null
   sleep 2
   ```

2. **Optional — clean obj folders if locks persist**:
   ```bash
   rm -rf /path/to/XerahS/ImageEditor/src/ShareX.ImageEditor/obj/Release
   ```
   This is most useful if you see `The process cannot access the file '...ShareX.ImageEditor.pdb' because it is being used by another process` (Avalonia AVLN9999 error).

---

### Phase 2: Run the Build Script

**IMPORTANT: Always redirect output to a log file.** The build takes 5-15 minutes and `command_status` produces **no output** during that time — the tool will appear to hang. Do NOT use `WaitDurationSeconds > 30` with this command.

**Start the build with log redirection:**
```bash
bash build/linux/package-linux.sh > build_output.log 2>&1
```

This runs in the background. Monitor progress by periodically reading the log with `view_file`:

```
view_file: /path/to/XerahS/build_output.log  (last 50 lines)
```

**Repeat every ~30 seconds** until you see one of:
- `Done! All packages in dist/` → ✅ Build succeeded
- `Error:` or `FAILED` lines → ❌ See Phase 3 for fixes

**Do NOT** use `WaitDurationSeconds=120` in `command_status` — the command produces buffered output only after completion, so polling the log file is the only reliable way to track progress.

---

### Phase 3: Handle Common Failures

#### 🔴 Failure: `No precompiled XAML found for XerahS.UI.App`
**Symptom** (at runtime, not build time):
```
Avalonia.Markup.Xaml.XamlLoadException: No precompiled XAML found for XerahS.UI.App, 
make sure to specify x:Class and include your XAML file as AvaloniaResource
```

**Root Cause**: A C# converter class referenced in an `.axaml` file uses the **wrong namespace**.  
Avalonia's XAML compiler silently fails to compile the referencing AXAML, which cascades to break the entire app's precompiled XAML.

**How to diagnose**:
- Check any recently added/modified converters under `ImageEditor/src/ShareX.ImageEditor/UI/Adapters/Converters/`
- Verify their C# `namespace` matches the AXAML `xmlns:converters` import:

  In `EditorView.axaml`:
  ```xml
  xmlns:converters="using:ShareX.ImageEditor.Converters"
  ```
  So all converter classes **must** declare:
  ```csharp
  namespace ShareX.ImageEditor.Converters;
  ```

**Fix**:
```csharp
// WRONG — will silently break Avalonia XAML precompilation:
namespace ShareX.ImageEditor.UI.Adapters.Converters;

// CORRECT — matches the xmlns:converters import in EditorView.axaml:
namespace ShareX.ImageEditor.Converters;
```

After fixing, rebuild from scratch.

---

#### 🔴 Failure: `AVLN9999: The process cannot access the file '...XerahS.Imgur.Plugin.pdb' because it is being used by another process`

**Root Cause**: A previous `dotnet publish` is still running in the background (e.g. from a backgrounded `&` command).

**Fix**:
```bash
pkill -f "dotnet publish"
pkill -f "package-linux.sh"
sleep 3
bash build/linux/package-linux.sh
```

---

#### 🔴 Failure: `MSB3026: Could not copy '...XerahS.Uploaders.dll' ... being used by another process`

**Root Cause**: Parallel plugin publishing is racing to write shared DLLs.

**Fix**: This is usually a transient retry (MSBuild will retry automatically). If it becomes fatal:
```bash
rm -rf src/XerahS.Uploaders/obj/Release
bash build/linux/package-linux.sh
```

---

#### 🔴 Failure: `Error: No plugins were published for linux-x64`

**Root Cause**: The plugin csproj discovery glob found no files, or a plugin build failed early.

**Check**:
```bash
find src/Plugins -mindepth 2 -maxdepth 2 -name "*.csproj"
```

Each plugin project under `src/Plugins/` needs a `plugin.json` in the same directory.

---

### Phase 4: Validation

After the build completes, verify the artifacts:

```bash
ls -lh dist/
```

**Expected output**:
```
XerahS-0.16.1-linux-x64.deb    ~90-120MB
XerahS-0.16.1-linux-arm64.deb  ~90-120MB
XerahS-0.16.1-linux-x64.rpm    ~90-120MB  (if rpmbuild installed)
XerahS-0.16.1-linux-arm64.rpm  ~90-120MB  (if rpmbuild installed)
```

Timestamps should match the current build session.

---

## Important Notes

### Why Avalonia XAML Precompilation Fails Silently
- Avalonia compiles `.axaml` files to IL at build time
- If a referenced type (e.g. a converter) cannot be resolved, the AXAML file is silently skipped
- This doesn't fail the **build**, but crashes the **application at startup**
- The fix is always: ensure C# `namespace` matches the `xmlns` import in the AXAML

### Key Build Parameters (from `package-linux.sh`)
- `-p:PublishSingleFile=true --self-contained true`: Main app ships as one binary
- `-p:OS=Linux -p:DefineConstants=LINUX`: Enables Linux-specific code paths
- `-p:EnableWindowsTargeting=true`: Required when cross-compiling on Linux due to shared project references
- Plugins publish with `--no-self-contained` to share the runtime with the main app

### Background Build Caution
- **Do not background the build script with `&`** unless you redirect output to a log file
- Multiple concurrent builds share `obj/` folders and will conflict
- Always kill previous builds before starting a new one

### stdout Buffering Issue
- `dotnet publish` progress may not appear in `command_status` tool output until the command finishes
- Redirect to a `.log` file and use `view_file` to check progress instead of waiting for command output

---

## Success Criteria
- ✅ Both `linux-x64` and `linux-arm64` `.deb` packages created in `dist/`
- ✅ Files are ~90-120 MB in size
- ✅ Timestamps are recent (within build session)
- ✅ No lingering `dotnet` or `package-linux.sh` processes
- ✅ App launches without `XamlLoadException` at startup

---

## Troubleshooting

| Symptom | Solution |
|---------|----------|
| `XamlLoadException: No precompiled XAML found` at startup | Check namespaces of all new converter classes — must match `xmlns:converters` in `.axaml` |
| `AVLN9999: file used by another process` | Kill all `dotnet publish` and `package-linux.sh` processes, retry |
| `MSB3026: Could not copy XerahS.Uploaders.dll` | Usually transient; if fatal, delete `src/XerahS.Uploaders/obj/Release` and retry |
| `Error: No plugins were published` | Check `src/Plugins/` structure and `plugin.json` presence in each plugin directory |
| ARM64 cross-compile fails | Ensure `linux-arm64` .NET SDK cross-compile support is installed; Fedora needs `dotnet-sdk-10.0` |
| `rpmbuild: command not found` | RPM skipped (not fatal); install with `sudo dnf install rpm-build` if needed |
| Build succeeds but app segfaults | SkiaSharp native library issue; **never bump SkiaSharp beyond 2.88.9** |

---

## Related Files
- Build script: [build/linux/package-linux.sh](../../../build/linux/package-linux.sh)
- Packaging tool: [build/linux/XerahS.Packaging/](../../../build/linux/XerahS.Packaging/)
- Version config: [Directory.Build.props](../../../Directory.Build.props)
- Main app project: [src/XerahS.App/XerahS.App.csproj](../../../src/XerahS.App/XerahS.App.csproj)
- Converters namespace reference: `ShareX.ImageEditor.Converters` (match all new converter classes to this)
