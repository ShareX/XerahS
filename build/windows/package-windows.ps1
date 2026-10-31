[CmdletBinding()]
param(
    # Publish and package ZIPs without requiring Inno Setup or WiX.
    [switch]$PortableOnly,

    # Architectures to package. CI runs one per job in parallel; local runs default to both.
    [ValidateSet("win-x64", "win-arm64")]
    [string[]]$Architectures = @("win-x64", "win-arm64")
)

$ErrorActionPreference = "Stop"

# This script builds Windows portable ZIPs and installers (Inno Setup required unless -PortableOnly).
# It also builds MSI packages using WiX Toolset v4+ when `wix` is available in PATH.
# Install WiX: dotnet tool install --global wix --version 6.0.2 ; wix extension add --global WixToolset.UI.wixext/6.0.2

# ---------------------------------------------------------------------------
# Helper: generate a WiX v4 ComponentGroup fragment for all files under a
# directory.  Sub-directory hierarchy is encoded via Component/@Subdirectory.
# Files whose root folder appears in ExcludeTopDirs are skipped.
# ---------------------------------------------------------------------------
function New-WixComponentGroupFragment {
    param (
        [string]   $SourceDir,
        [string]   $ComponentGroupId,
        [string]   $DirectoryId,
        [string[]] $ExcludeTopDirs = @()
    )

    $SourceDir = $SourceDir.TrimEnd('\', '/')

    $allFiles  = Get-ChildItem -Path $SourceDir -File -Recurse -ErrorAction SilentlyContinue
    $lines     = [System.Collections.Generic.List[string]]::new()
    $idx       = 0

    foreach ($file in $allFiles) {
        $relativePath = $file.FullName.Substring($SourceDir.Length + 1)
        $topDir       = ($relativePath -split '\\')[0]
        if ($ExcludeTopDirs -contains $topDir) { continue }

        $subDir      = [System.IO.Path]::GetDirectoryName($relativePath)
        $subdirAttr  = if ($subDir) { " Subdirectory=`"$subDir`"" } else { "" }
        $compId      = "${ComponentGroupId}_$idx"
        $idx++

        $lines.Add("      <Component Id=`"$compId`"$subdirAttr>")
        $lines.Add("        <File Source=`"$($file.FullName)`" />")
        $lines.Add("      </Component>")
    }

    $header = @(
        '<?xml version="1.0" encoding="utf-8"?>'
        '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">'
        '  <Fragment>'
        "    <ComponentGroup Id=`"$ComponentGroupId`" Directory=`"$DirectoryId`">"
    )
    $footer = @(
        '    </ComponentGroup>'
        '  </Fragment>'
        '</Wix>'
    )

    return ($header + $lines + $footer) -join "`n"
}

if ($env:OS -ne "Windows_NT") {
    $platform = if ($IsCoreClr) { [System.Environment]::OSVersion.Platform } else { "Unknown" }
    Write-Error "package-windows.ps1 requires Windows (Inno Setup). Current OS: $platform. Run this script on Windows."
    exit 1
}

# Use Join-Path for cross-platform path construction (works on PowerShell Core on any OS; script will exit above on non-Windows).
$root = Resolve-Path (Join-Path (Join-Path $PSScriptRoot "..") "..")
$project = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $root "src") "desktop") "app") "XerahS.App") "XerahS.App.csproj"
$issScript = Join-Path (Join-Path (Join-Path $root "build") "windows") "XerahS-setup.iss"
$outputDir = Join-Path $root "dist"

if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Find ISCC (Inno Setup Compiler) - Windows only
$programFilesX86 = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ProgramFilesX86)
$isccPath = Join-Path (Join-Path $programFilesX86 "Inno Setup 6") "ISCC.exe"
if (!$PortableOnly -and !(Test-Path $isccPath)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found at: $isccPath"
}

# WiX Toolset v4 — optional; MSI build is skipped with a warning if not found.
# WiX Toolset v4 - optional; MSI build is skipped with a warning if not found.
$wixCmd  = Get-Command wix -ErrorAction SilentlyContinue
$skipMsi = $PortableOnly -or $null -eq $wixCmd
if ($skipMsi -and !$PortableOnly) {
    Write-Warning "WiX CLI (wix.exe) not found in PATH - MSI packages will not be built."
    Write-Warning "Install: dotnet tool install --global wix --version 6.0.2"
    Write-Warning "Then add extension: wix extension add --global WixToolset.UI.wixext/6.0.2"
} elseif (!$skipMsi) {
    Write-Host "WiX CLI: $($wixCmd.Source)"
}
$wixScript = Join-Path (Join-Path (Join-Path $root "build") "windows") "XerahS-setup.wxs"

$version = ""
# Try to detect version from Directory.Build.props
$propsFile = Join-Path $root "Directory.Build.props"
if (Test-Path $propsFile) {
    $xml = [xml](Get-Content $propsFile)
    $versionNode = $xml.SelectSingleNode("//Version")
    if ($versionNode -and $versionNode.InnerText) {
        $version = $versionNode.InnerText.Trim()
    }
}

if ([string]::IsNullOrEmpty($version)) {
    # Fallback to msbuild
    $version = dotnet msbuild $project -getProperty:Version
    $version = $version.Trim()
}

Write-Host "Building XerahS version $version for Windows..."

function Invoke-ProjectRestoreForOS {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$OSValue
    )

    dotnet restore $ProjectPath -p:OS=$OSValue --disable-build-servers -p:nodeReuse=false -p:UseSharedCompilation=false -p:BuildInParallel=false /m:1
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE for $ProjectPath (OS=$OSValue)."
    }
}

function Invoke-ScopedIntermediateRestores {
    $imageEditorProject = Join-Path (Join-Path (Join-Path (Join-Path $root "ShareX.ImageEditor") "src") "ShareX.ImageEditor") "ShareX.ImageEditor.csproj"
    $uiProject = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $root "src") "desktop") "app") "XerahS.UI") "XerahS.UI.csproj"

    if (!(Test-Path $imageEditorProject)) {
        throw "ShareX.ImageEditor project not found: $imageEditorProject"
    }
    if (!(Test-Path $uiProject)) {
        throw "XerahS.UI project not found: $uiProject"
    }

    Write-Host "Restoring scoped intermediate assets for Windows packaging..."
    Invoke-ProjectRestoreForOS -ProjectPath $imageEditorProject -OSValue "Windows_NT"
    Invoke-ProjectRestoreForOS -ProjectPath $uiProject -OSValue "Windows_NT"
}

Invoke-ScopedIntermediateRestores

$archs = $Architectures

foreach ($arch in $archs) {
    Write-Host "`n-------------------------------------------"
    Write-Host "Building for $arch..."
    Write-Host "-------------------------------------------"

    # 1. Publish
    $publishOutput = Join-Path (Join-Path $root "build") "publish-temp-$arch"
    Write-Host "Publishing to $publishOutput..."
    # Ensure clean
    if (Test-Path $publishOutput) { Remove-Item -Recurse -Force $publishOutput }

    # Kill any lingering build processes before publishing to avoid file lock on ImageEditor.dll
    Get-Process | Where-Object {
        $_.Name -like '*VBCSCompiler*' -or $_.Name -like '*MSBuild*'
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    dotnet build-server shutdown | Out-Null

    # Enable PublishSingleFile=false to ensure DLLs are present for ISCC *.dll match
    # Pass SkipBundlePlugins=true to avoid path resolution bugs in custom MSBuild targets
    # Disable nodeReuse and UseSharedCompilation to avoid VBCSCompiler file locking on multi-TFM builds
    # /m:1 forces single-threaded build to prevent parallel TFM race conditions on ImageEditor.dll
    dotnet publish $project -c Release -p:OS=Windows_NT -r $arch -p:PublishSingleFile=false -p:SkipBundlePlugins=true -p:nodeReuse=false -p:UseSharedCompilation=false -p:BuildInParallel=false --disable-build-servers --self-contained true -o $publishOutput /m:1
    if ($LASTEXITCODE -ne 0) {
        throw "App publish failed for $arch with exit code $LASTEXITCODE."
    }

    $daemonExecutable = Join-Path $publishOutput "xerahs-watchfolder-daemon.exe"
    $daemonRuntimeConfig = Join-Path $publishOutput "xerahs-watchfolder-daemon.runtimeconfig.json"
    if (!(Test-Path $daemonExecutable)) {
        throw "Missing watch folder daemon executable in publish output: $daemonExecutable"
    }

    if (!(Test-Path $daemonRuntimeConfig)) {
        Write-Warning "Daemon runtimeconfig not found (single-file self-contained publish may omit it): $daemonRuntimeConfig"
    }

    # 1.5 Publish Plugins
    Write-Host "Publishing Plugins..."
    $pluginsDir = Join-Path $publishOutput "Plugins"
    if (!(Test-Path $pluginsDir)) { New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null }

    $pluginProjects = Get-ChildItem -Path (Join-Path (Join-Path (Join-Path $root "src") "desktop") "plugins") -Filter "*.csproj" -Recurse
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
        dotnet publish $plugin.FullName -c Release -p:OS=Windows_NT -r $arch -p:nodeReuse=false -p:UseSharedCompilation=false -p:BuildInParallel=false --disable-build-servers --self-contained false -o $pluginOutput /m:1
        if ($LASTEXITCODE -ne 0) {
            throw "Plugin publish failed for $($plugin.Name) ($arch) with exit code $LASTEXITCODE."
        }


    }

    dotnet build-server shutdown | Out-Null


    # 1.6 Deduplicate plugin files that already exist in main app
    Write-Host "Deduplicating plugin files..."
    $dedupStats = @{ Removed = 0; Errors = 0; BytesSaved = 0 }
    $maxRetries = 3
    $retryDelayMs = 500

    foreach ($pluginDir in Get-ChildItem -Path $pluginsDir -Directory) {
        $pluginFiles = Get-ChildItem -Path $pluginDir.FullName -File -ErrorAction SilentlyContinue
        foreach ($file in $pluginFiles) {
            $mainAppFile = Join-Path $publishOutput $file.Name
            if (Test-Path $mainAppFile) {
                $success = $false
                $attempts = 0
                while (-not $success -and $attempts -lt $maxRetries) {
                    $attempts++
                    try {
                        Remove-Item -Path $file.FullName -Force -ErrorAction Stop
                        $success = $true
                        $dedupStats.Removed++
                        $dedupStats.BytesSaved += $file.Length
                    }
                    catch {
                        if ($attempts -eq $maxRetries) {
                            Write-Warning "Failed to remove duplicate after $maxRetries attempts: $($file.Name)"
                            $dedupStats.Errors++
                        }
                        else {
                            Write-Host "  Retry $attempts/$maxRetries for: $($file.Name)"
                            Start-Sleep -Milliseconds $retryDelayMs
                        }
                    }
                }
            }
        }
    }
    $savedMB = [math]::Round($dedupStats.BytesSaved / 1MB, 2)
    Write-Host "Deduplication complete: Removed $($dedupStats.Removed) files, saved ${savedMB} MB, $($dedupStats.Errors) errors"

    # Package the same complete payload without adding portable.txt to the installer source.
    & (Join-Path $PSScriptRoot 'package-portable.ps1') -PublishDirectory $publishOutput `
        -Version $version -Runtime $arch -OutputDirectory $outputDir

    if (!$PortableOnly) {
    # 2. Compile Installer
    Write-Host "Compiling Installer with Inno Setup..."
    $setupBaseName = "XerahS-$version-$arch"
    $setupExe = "$setupBaseName.exe"
    
    # We override OutputDir to point directly to our dist folder and OutputBaseFilename for the requested naming.
    # We also override MyAppReleaseDirectory to ensure the compiler looks in the exact publish folder we just created.
    $archLog = "iscc_log_$arch.txt"
    $arg1 = "/dMyAppReleaseDirectory=$publishOutput"
    $arg2 = "/dOutputBaseFilename=$setupBaseName"
    $arg3 = "/dOutputDir=$outputDir"
    
    Write-Host "ISCC Arguments:"
    Write-Host "  $arg1"
    Write-Host "  $arg2"
    Write-Host "  $arg3"
    Write-Host "  $issScript"

    & $isccPath $arg1 $arg2 $arg3 $issScript | Out-File -FilePath $archLog -Encoding UTF8
    
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC Compiler failed with exit code $LASTEXITCODE. See $archLog for details."
    }

    $compiledSetup = Join-Path $outputDir $setupExe
    if (Test-Path $compiledSetup) {
        Write-Host "Success: Generated $setupExe in dist."
    } else {
         # Fallback search in case OutputDir override didn't behave as expected in this ISCC version
         $fallbackSearchDir = Join-Path $root "Output"
         $setupFiles = Get-ChildItem -Path $fallbackSearchDir -Filter "$setupBaseName.exe" -ErrorAction SilentlyContinue
         if ($setupFiles) {
            foreach ($file in $setupFiles) {
                Write-Host "Moving $($file.Name) from Output to dist..."
                Move-Item -Path $file.FullName -Destination $outputDir -Force
            }
         } else {
            throw "Failed to locate generated installer $setupExe"
         }
    }

    # 3. Build MSI with WiX Toolset v4 (parallel to the EXE installer)
    if (!$skipMsi) {
        Write-Host "`n-------------------------------------------"
        Write-Host "Building MSI for $arch with WiX..."
        Write-Host "-------------------------------------------"

        $wixTempDir = Join-Path $root "build\wix-temp-$arch"
        if (!(Test-Path $wixTempDir)) { New-Item -ItemType Directory -Force -Path $wixTempDir | Out-Null }

        try {
            # Generate AppFiles fragment: all files in publish output except the Plugins sub-directory.
            $harvestApp = Join-Path $wixTempDir "AppFiles.wxs"
            $appFragment = New-WixComponentGroupFragment `
                -SourceDir $publishOutput `
                -ComponentGroupId "AppFiles" `
                -DirectoryId "INSTALLFOLDER" `
                -ExcludeTopDirs @("Plugins")
            Set-Content -Path $harvestApp -Value $appFragment -Encoding UTF8

            # Generate PluginFiles fragment, or an empty one when no plugins exist.
            $harvestPlugins = Join-Path $wixTempDir "PluginFiles.wxs"
            $pluginsSourceDir = Join-Path $publishOutput "Plugins"
            if (Test-Path $pluginsSourceDir) {
                $pluginsFragment = New-WixComponentGroupFragment `
                    -SourceDir $pluginsSourceDir `
                    -ComponentGroupId "PluginFiles" `
                    -DirectoryId "PluginsFolder"
                Set-Content -Path $harvestPlugins -Value $pluginsFragment -Encoding UTF8
            } else {
                $emptyPlugins = '<?xml version="1.0" encoding="utf-8"?>' + "`n" +
                    '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">' + "`n" +
                    '  <Fragment>' + "`n" +
                    '    <ComponentGroup Id="PluginFiles" />' + "`n" +
                    '  </Fragment>' + "`n" +
                    '</Wix>'
                Set-Content -Path $harvestPlugins -Value $emptyPlugins -Encoding UTF8
            }

            # Map PowerShell RID to WiX architecture name.
            $wixArch     = if ($arch -eq "win-x64") { "x64" } else { "arm64" }
            $msiBaseName = "XerahS-$version-$arch"
            $msiOutput   = Join-Path $outputDir "$msiBaseName.msi"

            Write-Host "Running: wix build ... -arch $wixArch -o $msiOutput"
            wix build $wixScript $harvestApp $harvestPlugins `
                -d "Version=$version" `
                -arch $wixArch `
                -o $msiOutput

            if ($LASTEXITCODE -ne 0) {
                throw "WiX build failed for $arch with exit code $LASTEXITCODE"
            }

            if (Test-Path $msiOutput) {
                Write-Host "Success: Generated $msiBaseName.msi in dist."
            } else {
                throw "WiX build succeeded but output MSI not found: $msiOutput"
            }
        } finally {
            # Clean up temporary WiX fragment files regardless of success/failure.
            Remove-Item -Recurse -Force $wixTempDir -ErrorAction SilentlyContinue
        }
    }

    }

    # Cleanup temp publish folder
    Remove-Item -Recurse -Force $publishOutput
}

Write-Host "`nAll builds complete! Windows release packages in $outputDir"

