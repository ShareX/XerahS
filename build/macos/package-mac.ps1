#requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\..\.."
$project = Join-Path $root "src\XerahS.App\XerahS.App.csproj"
$outputDir = Join-Path $root "dist"

if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Get Version from Directory.Build.props
$buildPropsPath = Join-Path $root "Directory.Build.props"
$version = dotnet msbuild $buildPropsPath -getProperty:Version
$version = $version.Trim()
Write-Host "Building XerahS version $version for macOS..."

# Check for native library (pre-compiled)
$nativeLib = Join-Path $root "native\macos\libscreencapturekit_bridge.dylib"
if (!(Test-Path $nativeLib)) {
    Write-Warning "Native library not found at: $nativeLib"
    Write-Warning "Screen capture functionality will not work!"
    Write-Warning "Build on macOS first to generate the native library, or copy it manually."
    throw "Missing native library: $nativeLib"
} else {
    Write-Host "Using pre-compiled native library: $nativeLib"
    Write-Host "(To rebuild native library, run package-mac.sh on macOS)"
}

$archs = @("arm64", "x64")

foreach ($arch in $archs) {
    Write-Host "`n-------------------------------------------"
    Write-Host "Building for osx-$arch..."
    Write-Host "-------------------------------------------"

    $rid = "osx-$arch"
    
    # 1. Publish
    $publishDir = Join-Path $root "src\XerahS.App\bin\Release\net10.0\$rid\publish"
    Write-Host "Publishing to $publishDir..."
    
    # Ensure clean
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    # Publish with cross-compilation flag
    # The csproj automatically includes libscreencapturekit_bridge.dylib for osx RIDs
    dotnet publish $project -c Release -r $rid -p:PublishSingleFile=false --self-contained true -p:nodeReuse=false -p:SkipBundlePlugins=true -p:CrossCompile=true

    $appBundlePath = Join-Path $publishDir "XerahS.app"
    if (!(Test-Path $appBundlePath)) {
        throw "Error: .app bundle not found at $appBundlePath"
    }

    # 1.5 Publish Plugins
    Write-Host "Publishing Plugins..."
    $pluginsDir = Join-Path $appBundlePath "Contents\MacOS\Plugins"
    if (!(Test-Path $pluginsDir)) { New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null }

    $pluginProjects = Get-ChildItem -Path (Join-Path $root "src\Plugins") -Filter "*.csproj" -Recurse
    foreach ($plugin in $pluginProjects) {
        Write-Host "Publishing Plugin: $($plugin.Name)"
        
        # Try to determine plugin ID from plugin.json
        $pluginId = $plugin.BaseName
        $pluginJsonPath = Join-Path $plugin.Directory.FullName "plugin.json"
        if (Test-Path $pluginJsonPath) {
            try {
                $jsonContent = Get-Content $pluginJsonPath -Raw | ConvertFrom-Json
                if ($jsonContent.pluginId) {
                    $pluginId = $jsonContent.pluginId
                    Write-Host "  Found Plugin ID: $pluginId"
                }
            } catch {
                Write-Warning "  Failed to read plugin.json for $($plugin.Name)"
            }
        }

        $pluginOutput = Join-Path $pluginsDir $pluginId
        dotnet publish $plugin.FullName -c Release -r $rid --self-contained true -o $pluginOutput > $null

        # Deduplication - remove files that already exist in main app
        $mainAppDir = Join-Path $appBundlePath "Contents\MacOS"
        $pluginFiles = Get-ChildItem -Path $pluginOutput -File -ErrorAction SilentlyContinue
        foreach ($file in $pluginFiles) {
            $mainAppFile = Join-Path $mainAppDir $file.Name
            if (Test-Path $mainAppFile) {
                Remove-Item -Path $file.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }

    # 2. Package as .tar.gz
    $tarName = "XerahS-$version-mac-$arch.tar.gz"
    $tarPath = Join-Path $outputDir $tarName

    Write-Host "Creating archive: $tarName"
    
    # Use tar command (available in Windows 10/11 and via Git Bash)
    # tar -C [dir] -czf [archive] [file] to avoid including full path
    $tarArgs = "-C `"$publishDir`" -czf `"$tarPath`" XerahS.app"
    $tarProcess = Start-Process -FilePath "tar" -ArgumentList $tarArgs -Wait -NoNewWindow -PassThru
    
    if ($tarProcess.ExitCode -ne 0) {
        Write-Warning "tar command failed. Trying alternative method..."
        
        # Fallback: Use Compress-Archive then rename (creates .zip though)
        $zipPath = [System.IO.Path]::ChangeExtension($tarPath, ".zip")
        Compress-Archive -Path $appBundlePath -DestinationPath $zipPath -Force
        Write-Host "Created .zip archive instead: $([System.IO.Path]::GetFileName($zipPath))"
    } else {
        Write-Host "Success: Generated $tarName in dist."
    }
}

Write-Host "`nAll builds complete! Packages in $outputDir"
