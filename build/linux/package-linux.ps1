$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Write-Host $Description
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit $LASTEXITCODE): dotnet $($Arguments -join ' ')"
    }
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (!(Test-Path $Path)) {
        throw $Message
    }
}

function Validate-PublishOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDir
    )

    $appExecutable = Join-Path $PublishDir "XerahS"
    $daemonExecutable = Join-Path $PublishDir "xerahs-watchfolder-daemon"
    $daemonRuntimeConfig = Join-Path $PublishDir "xerahs-watchfolder-daemon.runtimeconfig.json"
    $omaxerahsExecutable = Join-Path $PublishDir "omaxerahs"
    $omaxerahsRuntimeConfig = Join-Path $PublishDir "omaxerahs.runtimeconfig.json"

    Assert-PathExists -Path $PublishDir -Message "Publish directory does not exist: $PublishDir"
    Assert-PathExists -Path $appExecutable -Message "Missing app executable in publish output: $appExecutable"
    Assert-PathExists -Path $daemonExecutable -Message "Missing daemon executable in publish output: $daemonExecutable"
    Assert-PathExists -Path $daemonRuntimeConfig -Message "Missing daemon runtimeconfig in publish output: $daemonRuntimeConfig"
    Assert-PathExists -Path $omaxerahsExecutable -Message "Missing omaxerahs executable in publish output: $omaxerahsExecutable"
    Assert-PathExists -Path $omaxerahsRuntimeConfig -Message "Missing omaxerahs runtimeconfig in publish output: $omaxerahsRuntimeConfig"
}

$root = Resolve-Path "$PSScriptRoot\..\.."
$project = Join-Path $root "src\desktop\app\XerahS.App\XerahS.App.csproj"
$outputDir = Join-Path $root "dist"
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Get Version from Directory.Build.props
$buildPropsPath = Join-Path $root "Directory.Build.props"
$version = & dotnet msbuild $buildPropsPath -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read version from $buildPropsPath"
}
$version = $version.Trim()
Write-Host "Building XerahS version $version for Linux..."

# 1. Clean & Publish
$publishDir = Join-Path $root "src\desktop\app\XerahS.App\bin\Release\net11.0\linux-x64\publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

Invoke-DotNet -Description "Running dotnet publish (linux-x64)..." -Arguments @(
    "build-server", "shutdown"
)
Invoke-DotNet -Description "Publishing XerahS app..." -Arguments @(
    "publish", $project,
    "-c", "Release",
    "-r", "linux-x64",
    "-p:OS=Linux",
    "-p:DefineConstants=LINUX",
    "-p:PublishSingleFile=true",
    "--self-contained", "true",
    "--disable-build-servers",
    "-p:nodeReuse=false",
    "-p:UseSharedCompilation=false",
    "-p:BuildInParallel=false",
    "-m:1",
    "-p:SkipBundlePlugins=true"
)
Validate-PublishOutput -PublishDir $publishDir

# 1.5 Publish Plugins
Write-Host "Publishing Plugins..."
$pluginsDir = Join-Path $publishDir "Plugins"
if (!(Test-Path $pluginsDir)) { New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null }

$pluginProjects = Get-ChildItem -Path (Join-Path $root "src\desktop\plugins") -Filter "*.csproj" -Recurse
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
    if (Test-Path $pluginOutput) { Remove-Item -Recurse -Force $pluginOutput }
    New-Item -ItemType Directory -Force -Path $pluginOutput | Out-Null

    Invoke-DotNet -Description "  Publishing Plugin: $($plugin.Name) ($pluginId)" -Arguments @(
        "publish", $plugin.FullName,
        "-c", "Release",
        "-r", "linux-x64",
        "-p:OS=Linux",
        "-o", $pluginOutput,
        "--no-self-contained",
        "-p:PublishSingleFile=false",
        "--disable-build-servers",
        "-p:nodeReuse=false",
        "-p:UseSharedCompilation=false",
        "-p:BuildInParallel=false",
        "-m:1"
    )

    if (!(Test-Path (Join-Path $pluginOutput "plugin.json")) -and (Test-Path $pluginJsonPath)) {
        Copy-Item $pluginJsonPath (Join-Path $pluginOutput "plugin.json")
    }

    # Cleanup: Remove files that already exist in the main app directory (deduplication)
    $pluginFiles = Get-ChildItem -Path $pluginOutput
    foreach ($file in $pluginFiles) {
        $mainAppFile = Join-Path $publishDir $file.Name
        if (Test-Path $mainAppFile) {
            Remove-Item $file.FullName -Recurse -Force
        }
    }
}

Validate-PublishOutput -PublishDir $publishDir
$pluginManifestCount = (Get-ChildItem -Path $pluginsDir -Filter "plugin.json" -Recurse -File | Measure-Object).Count
if ($pluginManifestCount -eq 0) {
    throw "No plugin manifests were found after plugin publish: $pluginsDir"
}

# 2. Package
Write-Host "Packaging..."
Write-Host "Note: rpmbuild is required to produce RPM packages."
$packagingTool = Join-Path $root "build\linux\XerahS.Packaging\XerahS.Packaging.csproj"
Invoke-DotNet -Description "Running Linux packaging tool..." -Arguments @(
    "run", "--project", $packagingTool, "--",
    $publishDir, $outputDir, $version, "linux-x64"
)

Write-Host "Done! Packages in $outputDir"


