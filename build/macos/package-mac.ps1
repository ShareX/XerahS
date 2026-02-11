#requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\..\.."
$project = Join-Path $root "src\XerahS.App\XerahS.App.csproj"
$outputDir = Join-Path $root "dist"

if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

function Convert-ToUnixPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath -match "^[A-Za-z]:\\") {
        $drive = $fullPath.Substring(0, 1).ToLowerInvariant()
        $rest = $fullPath.Substring(2) -replace "\\", "/"
        return "/$drive$rest"
    }

    return $fullPath -replace "\\", "/"
}

function Set-MacPlistStringKey {
    param(
        [Parameter(Mandatory = $true)][xml]$PlistXml,
        [Parameter(Mandatory = $true)][System.Xml.XmlNode]$DictNode,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $children = @($DictNode.ChildNodes)
    for ($i = 0; $i -lt $children.Count; $i++) {
        $child = $children[$i]
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq "key" -and $child.InnerText -eq $Key) {
            $valueNode = $null
            for ($j = $i + 1; $j -lt $children.Count; $j++) {
                if ($children[$j].NodeType -eq [System.Xml.XmlNodeType]::Element) {
                    $valueNode = $children[$j]
                    break
                }
            }

            if ($null -eq $valueNode -or $valueNode.Name -eq "key") {
                $newValueNode = $PlistXml.CreateElement("string")
                $newValueNode.InnerText = $Value
                if ($null -eq $valueNode) {
                    [void]$DictNode.AppendChild($newValueNode)
                } else {
                    [void]$DictNode.InsertBefore($newValueNode, $valueNode)
                }
            } elseif ($valueNode.Name -ne "string") {
                $replacementNode = $PlistXml.CreateElement("string")
                $replacementNode.InnerText = $Value
                [void]$DictNode.ReplaceChild($replacementNode, $valueNode)
            } else {
                $valueNode.InnerText = $Value
            }

            return
        }
    }

    $keyNode = $PlistXml.CreateElement("key")
    $keyNode.InnerText = $Key
    $valueNode = $PlistXml.CreateElement("string")
    $valueNode.InnerText = $Value
    [void]$DictNode.AppendChild($keyNode)
    [void]$DictNode.AppendChild($valueNode)
}

function Configure-MacBundleIcon {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$AppBundlePath
    )

    $iconSource = Join-Path $RootPath "src\XerahS.UI\Assets\Logo.icns"
    $resourcesDir = Join-Path $AppBundlePath "Contents\Resources"
    $plistPath = Join-Path $AppBundlePath "Contents\Info.plist"

    if (!(Test-Path $iconSource)) {
        Write-Warning "Icon not found at $iconSource. macOS app icon will be missing."
        return
    }

    if (!(Test-Path $plistPath)) {
        Write-Warning "Info.plist not found at $plistPath. macOS app icon will be missing."
        return
    }

    if (!(Test-Path $resourcesDir)) {
        New-Item -ItemType Directory -Force -Path $resourcesDir | Out-Null
    }

    Copy-Item -Path $iconSource -Destination (Join-Path $resourcesDir "Logo.icns") -Force

    [xml]$plistXml = Get-Content -Path $plistPath -Raw
    $dictNode = $plistXml.SelectSingleNode("/plist/dict")
    if ($null -eq $dictNode) {
        throw "Invalid Info.plist format in $plistPath (missing /plist/dict)."
    }

    Set-MacPlistStringKey -PlistXml $plistXml -DictNode $dictNode -Key "CFBundleIconFile" -Value "Logo"
    Set-MacPlistStringKey -PlistXml $plistXml -DictNode $dictNode -Key "CFBundleIconName" -Value "Logo"
    $plistXml.Save($plistPath)

    Write-Host "Configured macOS icon metadata for $AppBundlePath"
}

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
    dotnet publish $project -c Release -r $rid -p:PublishSingleFile=false --self-contained true -p:nodeReuse=false -p:SkipBundlePlugins=true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid with exit code $LASTEXITCODE."
    }

    $appBundlePath = Join-Path $publishDir "XerahS.app"
    if (!(Test-Path $appBundlePath)) {
        throw "Error: .app bundle not found at $appBundlePath"
    }

    Configure-MacBundleIcon -RootPath $root -AppBundlePath $appBundlePath

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
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for plugin $($plugin.Name) on $rid with exit code $LASTEXITCODE."
        }

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
    
    $gitBash = "C:\Program Files\Git\bin\bash.exe"
    if (Test-Path $gitBash) {
        # Windows builds lose Unix execute permissions; enforce mode in archive.
        $publishDirUnix = Convert-ToUnixPath $publishDir
        $tarPathUnix = Convert-ToUnixPath $tarPath
        $tarCommand = "set -e; tar -C '$publishDirUnix' --mode='a+rx,u+w' -czf '$tarPathUnix' XerahS.app"
        & $gitBash -lc $tarCommand

        if ($LASTEXITCODE -ne 0) {
            throw "GNU tar packaging failed for $rid."
        }

        Write-Host "Success: Generated $tarName in dist."
    } else {
        Write-Warning "Git Bash not found at '$gitBash'. Falling back to Windows tar."
        Write-Warning "The app may fail to open on macOS due to missing executable permissions."

        # Fallback tar -C [dir] -czf [archive] [file] to avoid including full path
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
}

Write-Host "`nAll builds complete! Packages in $outputDir"
