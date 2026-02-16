# Building XerahS Mobile Projects on Linux

**Copyright (c) 2007-2026 ShareX Team**

## Prerequisites

### 1. Android Workload Installation
The Android workload was successfully installed using a custom temp directory to avoid tmpfs space issues:

```bash
mkdir -p ~/tmp-dotnet-workload
export TMPDIR=~/tmp-dotnet-workload
sudo -E TMPDIR=$TMPDIR dotnet workload install android --skip-sign-check
```

### 2. Java Development Kit (JDK)
Android builds require OpenJDK 21:

```bash
sudo dnf install -y java-21-openjdk-devel
```

**Note**: JDK 25 is not supported by the Android SDK. JDK 21 is the required version.

### 3. Android SDK Dependencies
Install Android platform SDK for API level 36:

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH="$JAVA_HOME/bin:$PATH"
dotnet build src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj \
  -t:InstallAndroidDependencies \
  -f net10.0-android \
  "-p:AndroidSdkDirectory=/home/$USER/Android/Sdk" \
  "-p:AcceptAndroidSDKLicenses=true"
```

## Building

### Environment Setup
Before building, set the Java environment:

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH="$JAVA_HOME/bin:$PATH"
```

### Build Commands

#### XerahS.Mobile.UI (Shared Library)
```bash
dotnet build src/XerahS.Mobile.UI/XerahS.Mobile.UI.csproj
```

#### XerahS.Mobile.Android
```bash
dotnet build src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj
```

#### XerahS.Mobile.Maui (Android Target)
```bash
dotnet build src/XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj
```

### Build Script
A convenience script is available at the repository root:

```bash
./build-mobile-android.sh
```

## Platform-Specific Configuration

### XerahS.Mobile.Maui Conditional Targets
The MAUI project has been configured to build only Android targets on Linux:

```xml
<!-- On macOS build both Android and iOS, on Linux only Android -->
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">net10.0-android;net10.0-ios</TargetFrameworks>
<TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('osx'))">net10.0-android</TargetFrameworks>
```

This allows the project to build on Linux without iOS workload dependencies, while still supporting both platforms on macOS.

## Limitations on Linux

### iOS Projects
The following projects **cannot** be built on Linux (macOS required):
- XerahS.Mobile.iOS
- XerahS.Mobile.iOS.ShareExtension

iOS workloads are only available on macOS.

### MAUI Multi-Targeting
The MAUI project requires conditional targeting logic to exclude iOS on Linux. This was resolved by adding platform-specific `TargetFrameworks` conditions.

## Known Warnings

### SkiaSharp Page Size (XA0141)
Both Android projects show warnings about 16 KB page sizes in SkiaSharp native libraries:

```
warning XA0141: Android 16 will require 16 KB page sizes, shared library 'libSkiaSharp.so' 
does not have a 16 KB page size.
```

**Impact**: These are forward-looking warnings for Android 16. The current builds work correctly on Android devices up to API level 35.

**Resolution**: This will be resolved when SkiaSharp 3.x is adopted (currently blocked by version constraints).

## Build Results

✅ **XerahS.Mobile.UI** - Builds successfully  
✅ **XerahS.Mobile.Android** - Builds successfully (2 warnings)  
✅ **XerahS.Mobile.Maui** (Android) - Builds successfully (2 warnings)  
❌ **XerahS.Mobile.iOS** - Requires macOS  
❌ **XerahS.Mobile.iOS.ShareExtension** - Requires macOS

## Troubleshooting

### "No space left on device" during workload installation
The default `/tmp` is a RAM-based tmpfs with limited space. Use a custom temp directory on your home partition:

```bash
mkdir -p ~/tmp-dotnet-workload
export TMPDIR=~/tmp-dotnet-workload
sudo -E TMPDIR=$TMPDIR dotnet workload install android --skip-sign-check
```

### "Building with JDK version X is not supported"
Ensure JDK 21 is installed and properly set in `JAVA_HOME`:

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH="$JAVA_HOME/bin:$PATH"
java -version  # Should show 21.x.x
```

### "Could not find android.jar for API level 36"
Install Android dependencies:

```bash
dotnet build src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj \
  -t:InstallAndroidDependencies \
  -f net10.0-android \
  "-p:AcceptAndroidSDKLicenses=true"
```

## See Also

- [Coding Standards](CODING_STANDARDS.md)
- [Testing Guidelines](TESTING.md)
- [Porting Guide](../architecture/PORTING_GUIDE.md)
