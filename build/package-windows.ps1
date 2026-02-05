$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "src\XerahS.App\XerahS.App.csproj"
$issScript = Join-Path $root "InnoSetup\XerahS-setup.iss"
$outputDir = Join-Path $root "dist"

if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Find ISCC (Inno Setup Compiler)
$isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (!(Test-Path $isccPath)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found at: $isccPath"
}

$version = ""
# Try to detect version from Directory.Build.props
$propsFile = Join-Path $root "Directory.Build.props"
if (Test-Path $propsFile) {
    $xml = [xml](Get-Content $propsFile)
    if ($xml.Project.PropertyGroup.Version) {
        $version = $xml.Project.PropertyGroup.Version
    }
}

if ([string]::IsNullOrEmpty($version)) {
    # Fallback to msbuild
    $version = dotnet msbuild $project -getProperty:Version
    $version = $version.Trim()
}

Write-Host "Building XerahS version $version for Windows..."

$archs = @("win-x64", "win-arm64")

foreach ($arch in $archs) {
    Write-Host "`n-------------------------------------------"
    Write-Host "Building for $arch..."
    Write-Host "-------------------------------------------"

    # 1. Publish
    $publishOutput = Join-Path $root "build\publish-temp-$arch"
    Write-Host "Publishing to $publishOutput..."
    # Ensure clean
    if (Test-Path $publishOutput) { Remove-Item -Recurse -Force $publishOutput }

    # Enable PublishSingleFile=false to ensure DLLs are present for ISCC *.dll match
    # Pass SkipBundlePlugins=true to avoid path resolution bugs in custom MSBuild targets
    # Disable nodeReuse to avoid file locking issues
    dotnet publish $project -c Release -p:OS=Windows_NT -r $arch -p:PublishSingleFile=false -p:SkipBundlePlugins=true -p:nodeReuse=false --self-contained true -o $publishOutput

    # 1.5 Publish Plugins
    Write-Host "Publishing Plugins..."
    $pluginsDir = Join-Path $publishOutput "Plugins"
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
        dotnet publish $plugin.FullName -c Release -p:OS=Windows_NT -r $arch -p:nodeReuse=false --self-contained false -o $pluginOutput

        # Note: Deduplication removed to prevent intermittent file locking issues during build loop
    }

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

    # Cleanup temp publish folder
    Remove-Item -Recurse -Force $publishOutput
}

Write-Host "`nAll builds complete! Installers in $outputDir"
