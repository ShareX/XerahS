$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\..\.."
$project = Join-Path $root "src\XerahS.App\XerahS.App.csproj"
$uiProject = Join-Path $root "src\XerahS.UI\XerahS.UI.csproj"
$outputDir = Join-Path $root "dist"
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Get Version from Directory.Build.props
$buildPropsPath = Join-Path $root "Directory.Build.props"
$version = dotnet msbuild $buildPropsPath -getProperty:Version
$version = $version.Trim()
Write-Host "Building XerahS version $version for Linux..."

# 1. Clean & Publish
$publishDir = Join-Path $root "src\XerahS.App\bin\Release\net10.0\linux-x64\publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

Write-Host "Running dotnet publish..."
dotnet publish $project -c Release -r linux-x64 -p:OS=Linux -p:DefineConstants=LINUX -p:PublishSingleFile=true --self-contained true

# 1.5 Publish Plugins
Write-Host "Publishing Plugins..."
$pluginsDir = Join-Path $publishDir "Plugins"
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
    # Note: Plugins should probably target strict linux-x64 if they contain native dependencies, or portable if not.
    # Using linux-x64 to match main app.
    dotnet publish $plugin.FullName -c Release -r linux-x64 -p:OS=Linux -o $pluginOutput

    # Cleanup: Remove files that already exist in the main app directory (deduplication)
    $pluginFiles = Get-ChildItem -Path $pluginOutput
    foreach ($file in $pluginFiles) {
        $mainAppFile = Join-Path $publishDir $file.Name
        if (Test-Path $mainAppFile) {
            Remove-Item $file.FullName -Recurse -Force
        }
    }
}

# 2. Package
Write-Host "Packaging..."
Write-Host "Note: rpmbuild is required to produce RPM packages."
$packagingTool = Join-Path $root "build\linux\XerahS.Packaging\XerahS.Packaging.csproj"
dotnet run --project $packagingTool -- $publishDir $outputDir $version "linux-x64"

Write-Host "Done! Packages in $outputDir"


