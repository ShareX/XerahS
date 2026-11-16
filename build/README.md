# XerahS Build System Documentation

This document describes the build system structure and how builds work for each operating system.

---

## Directory Structure

```
build/
├── README.md                          # This file
├── windows/                           # Windows build scripts
│   ├── package-windows.ps1           # Main PowerShell build script
│   ├── XerahS-setup.iss              # Inno Setup installer script
│   ├── scoop/                        # Scoop manifests
│   │   └── scoop.json                # Scoop manifest for XerahS
│   ├── winget/                       # WinGet manifests
│   │   └── generate-winget.ps1       # Script to generate WinGet manifests
│   └── chocolatey/                   # Chocolatey packages
│       ├── Sync-ChocolateyPackage.ps1# Sync + pack Chocolatey package metadata
│       ├── xerahs.nuspec             # Chocolatey package definition
│       └── tools/                    # Chocolatey install/uninstall scripts
├── linux/                             # Linux build scripts
│   ├── package-linux.ps1             # PowerShell wrapper for Linux build (Windows)
│   ├── package-linux.sh              # Bash script for Linux build (Linux/macOS)
│   ├── package-aur.sh                # Bash script for building a local Arch package
│   ├── aur/                          # Arch packaging metadata
│   │   └── xerahs-git/
│   │       └── PKGBUILD             # Local PKGBUILD used by package-aur.sh
│   ├── repo-staging/                  # PPA / COPR / OBS templates (#253)
│   └── XerahS.Packaging/             # C# packaging tool
│       ├── Program.cs                # Packaging logic (tar.gz, .deb, .rpm)
│       └── XerahS.Packaging.csproj   # Project file
├── macos/                             # macOS build scripts
│   ├── package-mac.ps1               # PowerShell script for macOS build (Windows)
│   └── package-mac.sh                # Bash script for macOS build (macOS)
└── android/                           # Android/Mobile build scripts
    ├── build-android.sh              # Bash script for Android build (Linux)
    ├── build-android.ps1             # PowerShell script for Android build (Windows)
    └── README.md                     # Detailed Android build documentation
```

---

## Windows Build

### Files
- **`package-windows.ps1`** - PowerShell build orchestrator
- **`XerahS-setup.iss`** - Inno Setup installer definition

### How It Works

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Windows Build Flow                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1. Detect version from Directory.Build.props                           │
│                              ↓                                          │
│  2. For each architecture (win-x64, win-arm64):                         │
│                              ↓                                          │
│     a. dotnet publish (main app) → build/publish-temp-{arch}/           │
│                              ↓                                          │
│     b. Publish Plugins to Plugins/ subfolder                            │
│        • Reads plugin.json for pluginId                                 │
│        • Publishes each plugin to Plugins/{pluginId}/                   │
│                              ↓                                          │
│     c. Deduplicate plugin files                                         │
│        • Removes duplicate DLLs already in main app                     │
│        • Saves ~170 MB per architecture                                 │
│                              ↓                                          │
│     d. ISCC.exe (Inno Setup)                                            │
│        • /dMyAppReleaseDirectory={publish-temp}                         │
│        • /dOutputBaseFilename=XerahS-{version}-{arch}                   │
│        • /dOutputDir={dist}                                             │
│                              ↓                                          │
│  3. Output: dist/XerahS-{version}-{arch}.exe                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key Features
- **Dual architecture support**: Builds for both x64 and ARM64
- **Plugin bundling**: Includes 5 plugins (amazons3, auto, gist, imgur, paste2)
- **File deduplication**: Saves space by removing duplicate DLLs from plugins
- **Inno Setup integration**: Creates professional Windows installers

### Requirements
- Inno Setup 6 (installed at `%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`)
- .NET SDK 10.0+

### Package Managers
The `build/windows` directory also contains resources for submitting XerahS to package managers.
- **Scoop**: `build/windows/scoop/scoop.json`
- **WinGet**: `build/windows/winget/generate-winget.ps1` (Generates manifests to `manifests/` subdir)
- **Chocolatey**: `build/windows/chocolatey/`
  - `Sync-ChocolateyPackage.ps1` updates nuspec metadata, installer checksums, and `VERIFICATION.txt` from an existing GitHub release.
  - `Test-ChocolateyPackage.ps1` smoke-tests the generated package with local `choco install` and `choco uninstall`.
  - `tools/chocolateyBeforeModify.ps1` stops running XerahS processes before upgrade or uninstall.
  - Add `-Pack` to generate `xerahs.<version>.nupkg`.
  - Tag releases automatically build, smoke-test, and attach the `.nupkg` to the GitHub release in `release-build-all-platforms.yml`.

---

## Linux Build

### Files
- **`package-linux.ps1`** - PowerShell wrapper (Windows hosts)
- **`package-linux.sh`** - Bash script (Linux/macOS hosts)
- **`package-aur.sh`** - Bash script for building a reusable local `xerahs-git` Arch package
- **`aur/xerahs-git/PKGBUILD`** - PKGBUILD consumed by `makepkg`
- **`XerahS.Packaging/`** - C# packaging tool

### How It Works

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Linux Build Flow                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1. Detect version from Directory.Build.props                           │
│                              ↓                                          │
│  2. dotnet publish (main app)                                           │
│     • Runtime: linux-x64                                                │
│     • Single file: true                                                 │
│     • Self-contained: true                                              │
│     → src/desktop/app/XerahS.App/bin/Release/net11.0/linux-x64/publish/ │
│                              ↓                                          │
│  3. Publish Plugins to Plugins/ subfolder                               │
│     • Same process as Windows                                           │
│     • Deduplicates files against main app                               │
│                              ↓                                          │
│  4. XerahS.Packaging tool creates:                                      │
│                              ↓                                          │
│     ┌─────────────────┬──────────────────────────────────────────┐     │
│     │   Tarball       │ XerahS-{version}-linux-x64.tar.gz        │     │
│     │   (.tar.gz)     │ Portable, extract and run                │     │
│     ├─────────────────┼──────────────────────────────────────────┤     │
│     │   Debian        │ XerahS-{version}-linux-x64.deb           │     │
│     │   Package       │ Installs to /usr/lib/xerahs/             │     │
│     │   (.deb)        │ Creates /usr/bin/xerahs wrapper          │     │
│     │                 │ Desktop entry + icon included            │     │
│     ├─────────────────┼──────────────────────────────────────────┤     │
│     │   RPM Package   │ XerahS-{version}-linux-x64.rpm           │     │
│     │   (.rpm)        │ For Fedora/RHEL/CentOS/SUSE              │     │
│     │                 │ Requires rpmbuild tool                   │     │
│     ├─────────────────┼──────────────────────────────────────────┤     │
│     │   AppImage      │ XerahS-{version}-linux-x64.AppImage      │     │
│     │                 │ Portable, chmod +x and run               │     │
│     │                 │ Also built for linux-arm64               │     │
│     └─────────────────┴──────────────────────────────────────────┘     │
│                              ↓                                          │
│  5. Individual plugin .zip files also created                           │
│     • {pluginId}-{version}-linux-x64.zip                                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Package Details

| Package Type | Install Location | Usage |
|-------------|------------------|-------|
| `.tar.gz` | User choice | Extract and run `./XerahS` |
| `.deb` | `/usr/lib/xerahs/` | `sudo dpkg -i xerahs.deb` |
| `.rpm` | `/usr/lib/xerahs/` | `sudo rpm -i xerahs.rpm` |

### Requirements
- .NET SDK 10.0+
- For RPM: `rpmbuild` tool (optional)

### Arch Linux Package Build

For local Arch/EndeavourOS packaging, use:

```bash
./build/linux/package-aur.sh
```

This script:
- builds the Linux release payload through `package-linux.sh`
- runs `makepkg` with the reusable PKGBUILD at `build/linux/aur/xerahs-git/PKGBUILD`
- copies the resulting `.pkg.tar.zst` package into `dist/aur/`

---

## macOS Build

### Files
- **`package-mac.ps1`** - PowerShell script for cross-compilation from Windows
- **`package-mac.sh`** - Bash script for building on macOS

### How It Works

#### Option 1: Build from Windows (Cross-Compilation)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    macOS Build Flow (from Windows)                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1. Detect version from Directory.Build.props                           │
│                              ↓                                          │
│  2. Verify pre-compiled native library exists                           │
│     • native/macos/libscreencapturekit_bridge.dylib                     │
│     • (Compile on macOS first if update needed)                         │
│                              ↓                                          │
│  3. For each architecture (osx-arm64, osx-x64):                         │
│                              ↓                                          │
│     a. dotnet publish with -p:CrossCompile=true                         │
│        • Uses net11.0 (not net11.0-windows...)                          │
│        • References XerahS.Platform.MacOS (not Windows)                 │
│                              ↓                                          │
│     b. Create .app bundle structure                                     │
│        XerahS.app/Contents/MacOS/                                       │
│                              ↓                                          │
│     c. Publish Plugins to Plugins/ subfolder                            │
│        • Same process as other platforms                                │
│                              ↓                                          │
│     d. Package as .tar.gz                                               │
│                              ↓                                          │
│  4. Output: dist/XerahS-{version}-mac-{arch}.tar.gz                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

#### Option 2: Build from macOS (Native)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      macOS Build Flow (from macOS)                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1. Detect version from Directory.Build.props                           │
│                              ↓                                          │
│  2. Build native ScreenCaptureKit library                               │
│     • cd native/macos && make                                           │
│     • Produces libscreencapturekit_bridge.dylib                         │
│                              ↓                                          │
│  3. dotnet publish (triggers CreateMacOSAppBundle target)               │
│                              ↓                                          │
│  4. Plugins, packaging same as cross-compile                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Cross-Compilation (`CrossCompile` Property)

The `-p:CrossCompile=true` flag enables building macOS/Linux binaries from Windows:

| Setting | Normal (Windows) | Cross-Compile (macOS/Linux) |
|---------|------------------|----------------------------|
| TargetFramework | `net11.0-windows10.0.26100.0` | `net11.0` |
| Platform Reference | `XerahS.Platform.Windows` | `XerahS.Platform.MacOS/Linux` |
| Preprocessor | `WINDOWS` defined | `WINDOWS` NOT defined |
| App Bundle | N/A | Created for macOS |

### Native Library Management

| Script | Native Library Source | Action |
|--------|----------------------|--------|
| `package-mac.sh` (macOS) | Source code | Compiles with `make` |
| `package-mac.ps1` (Windows) | Pre-compiled binary | Copies existing `.dylib` |

**To update the native library:**
1. Run `package-mac.sh` on macOS (compiles latest)
2. Commit the updated `libscreencapturekit_bridge.dylib`
3. Windows builds will use the updated binary

### Requirements

**For `package-mac.ps1` (Windows):**
- .NET SDK 10.0+
- Pre-compiled `native/macos/libscreencapturekit_bridge.dylib`

**For `package-mac.sh` (macOS):**
- macOS with Xcode Command Line Tools
- .NET SDK 10.0+

---

## Android/Mobile Build

### Files
- **`build-android.sh`** - Bash script for building Android apps (Linux)
- **`build-android.ps1`** - PowerShell script for building Android apps (Windows)
- **`README.md`** - Comprehensive Android build documentation

### How It Works

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      Android Build Flow                                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1. Detect version from Directory.Build.props                           │
│                              ↓                                          │
│  2. Configure Java environment                                          │
│     • Set JAVA_HOME to JDK 21                                           │
│     • Verify Java version                                               │
│                              ↓                                          │
│  3. Build XerahS.Mobile.Ava (Avalonia Android)                          │
│     • dotnet build -c Release -f net11.0-android                        │
│     → src/mobile-experimental/XerahS.Mobile.Ava/bin/.../net11.0-android/ │
│                              ↓                                          │
│  4. Build XerahS.Mobile.Maui (MAUI/Android)                             │
│     • dotnet build -c Release -f net11.0-android                        │
│     → src/mobile-experimental/XerahS.Mobile.Maui/bin/.../net11.0-android/│
│                              ↓                                          │
│  6. Copy APKs to dist/android/                                          │
│     • XerahS-{version}-Android.apk                                      │
│     • XerahS-{version}-MAUI-Android.apk                                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Platform-Specific Configuration

The MAUI project uses conditional targeting to support both platforms:

```xml
<!-- Build Android on all platforms, iOS only on macOS -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">
  net11.0-android;net11.0-ios
</TargetFrameworks>
<TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('osx'))">
  net11.0-android
</TargetFrameworks>
```

### Requirements

**For Android builds (Linux):**
- .NET SDK 10.0+ with Android workload
- OpenJDK 21 (not JDK 25!)
- Android SDK Platform API Level 36

**For Android builds (Windows):**
- .NET SDK 10.0+ with Android workload
- Microsoft JDK 21
- Android SDK Platform API Level 36

**Installation:**
See `build/android/README.md` for detailed setup instructions including:
- Android workload installation (requires custom temp directory on Linux)
- JDK 21 installation
- Android SDK dependencies installation

### iOS Builds

iOS projects (`XerahS.Mobile.iOS`, `XerahS.Mobile.iOS.ShareExtension`) **require macOS** and cannot be built on Linux or Windows. The MAUI project automatically excludes iOS targets on non-macOS platforms.

---

## Shared Plugin Build Process

All platforms use the same plugin discovery and build logic:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Plugin Build Flow                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  src/desktop/plugins/                                            │
│  ├── AmazonS3.Plugin/                                            │
│  │   ├── XerahS.AmazonS3.Plugin.csproj                           │
│  │   └── plugin.json                                             │
│  ├── Auto.Plugin/                                                │
│  │   └── plugin.json                                             │
│  └── ...                                                         │
│                                                                  │
│  Build script:                                                   │
│  1. Find all .csproj in src/desktop/plugins/                     │
│  2. Read plugin.json → extract "pluginId"                       │
│  3. dotnet publish to Plugins/{pluginId}/                       │
│  4. Remove files that already exist in main app                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Current Plugins

| Plugin ID | Name | Description |
|-----------|------|-------------|
| `amazons3` | Amazon S3 Uploader | Upload files to Amazon S3 buckets |
| `auto` | Auto Destination | Automatic upload destination selection |
| `gist` | GitHub Gist Text Uploader | Upload text/code to GitHub Gist |
| `imgur` | Imgur Uploader | Upload images to Imgur |
| `paste2` | Paste2 Text Uploader | Upload text to Paste2 service |

---

## Output Directory

All builds place their final artifacts in the `dist/` folder:

```
dist/
├── Windows
│   ├── XerahS-0.14.3-win-x64.exe
│   └── XerahS-0.14.3-win-arm64.exe
│
├── Linux
│   ├── XerahS-0.14.3-linux-x64.tar.gz
│   ├── XerahS-0.14.3-linux-x64.deb
│   ├── XerahS-0.14.3-linux-x64.rpm
│   ├── XerahS-0.14.3-linux-x64.AppImage
│   ├── amazons3-0.14.3-linux-x64.zip
│   ├── auto-0.14.3-linux-x64.zip
│   ├── gist-0.14.3-linux-x64.zip
│   ├── imgur-0.14.3-linux-x64.zip
│   └── paste2-0.14.3-linux-x64.zip
│
└── macOS
    ├── XerahS-0.14.3-mac-arm64.tar.gz  (Apple Silicon)
    └── XerahS-0.14.3-mac-x64.tar.gz    (Intel Mac)
```

---

## Quick Reference

### Build Commands

| Platform | Command | Host OS | Native Library |
|----------|---------|---------|----------------|
| Windows | `.\build\windows\package-windows.ps1` | Windows | N/A |
| Linux | `.\build\linux\package-linux.ps1` | Windows | N/A |
| Linux | `./build/linux/package-linux.sh` | Linux/macOS | N/A |
| macOS | `.\build\macos\package-mac.ps1` | Windows | Pre-compiled |
| macOS | `./build/macos/package-mac.sh` | macOS | Compiled from source |

### Version Detection

All scripts read version from `Directory.Build.props`:
```xml
<Version>0.14.3</Version>
```

### Common Build Flags

| Flag | Purpose |
|------|---------|
| `-c Release` | Release configuration |
| `-p:OS={OS}` | Target OS (Windows_NT, Linux, macOS) |
| `-r {runtime}` | Runtime identifier (win-x64, linux-x64, osx-x64, etc.) |
| `-p:PublishSingleFile=true/false` | Single executable vs multiple files |
| `--self-contained true/false` | Include .NET runtime |
| `-p:CrossCompile=true` | Enable cross-compilation from Windows to macOS/Linux |
| `-p:SkipBundlePlugins=true` | Skip automatic plugin bundling |
| `-p:nodeReuse=false` | Disable MSBuild node reuse (prevents file locking) |

---

## Troubleshooting

### Windows
- **ISCC not found**: Install Inno Setup 6 at default location
- **File locked**: Script disables `nodeReuse` to prevent file locking

### Linux
- **rpmbuild not found**: RPM package will be skipped (others still built)
- **Permission errors**: Ensure `dotnet` is in PATH

### macOS (Cross-Compile from Windows)
- **Native library not found**: Run `package-mac.sh` on macOS first to compile `libscreencapturekit_bridge.dylib`, then commit it
- **Screen capture not working**: Native library is outdated - rebuild on macOS

### macOS (Build on macOS)
- **make: command not found**: Install Xcode Command Line Tools (`xcode-select --install`)
- **Codesign issues**: May need to disable SIP or sign with developer cert
- **Notarization**: Required for distribution outside App Store

---

## Related Documentation

- `../DEVELOPER_README.md` - General development setup
- `../.github/skills/git-workflow/SKILL.md` - Release procedures
- `../docs/architecture/PORTING_GUIDE.md` - Platform abstractions
- `../native/macos/README_NATIVE.md` - Native macOS library documentation
