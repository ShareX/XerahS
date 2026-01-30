$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
$project = Join-Path $root "src\XerahS.App\XerahS.App.csproj"
$issScript = Join-Path $root "InnoSetup\XerahS-setup.iss"
$outputDir = Join-Path $root "dist"
$innoOutputDir = Join-Path $root "..\Output" # Output dir is ..\..\ from InnoSetup folder, which means ..\ from Project Root

if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

# Find ISCC (Inno Setup Compiler)
$isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (!(Test-Path $isccPath)) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found at: $isccPath"
}

# Get Version (Optional, ISCC extracts it from file, but good for logging)
# $version = dotnet msbuild $project -getProperty:Version

Write-Host "Building XerahS for Windows..."

# 1. Publish
# Note: The .iss expects the output in src\XerahS.App\bin\Release\net10.0-windows10.0.26100.0 without "publish" folder appended?
# Checking .iss line 4: MyAppReleaseDirectory ... "src\XerahS.App\bin\Release\net10.0-windows10.0.26100.0"
# dotnet publish usually puts it in bin\Release\target\publish. 
# But the .iss seems to point to the build output directory, not publish directory?
# "bin\Release\net10.0-windows..." 
# Let's check if we should publish to that specific directory or just build.
# Usually for an installer you want 'publish' output to get all dependencies.
# If the .iss points to the build output, it might miss dependencies if not self-contained or if they are not copied to output.
# However, the user said "automatically build the RELEASE version".
# Let's try to stick to what the .iss expects. If it points to `bin\Release\net...`, a `dotnet build -c Release` should populate that.
# IF we want self-contained or single-file, we usually use publish. 
# The .iss file at line 4: ... "src\XerahS.App\bin\Release\net10.0-windows10.0.26100.0". 
# This looks like standard build output.
# I will run `dotnet publish` but output to that directory to ensure we have a clean full set of files, 
# OR just run `dotnet build -c Release`.
# Given the user wants "AUTOMATICALLY BUILD", `dotnet publish` is safer for distribution to ensure all deps are present.
# But if I publish to `.../publish` and the .iss looks in `.../`, it won't see the published files.
# AND the .iss copies `*.dll` etc.
# I will use `dotnet publish` and force the output path to match what the .iss expects, ensuring a self-contained/ready-to-deploy folder.
# BUT wait, the .iss defines MyAppReleaseDirectory. 
# I shouldn't change the .iss if I can avoid it.
# I will run `dotnet publish` and use `-o` to output exactly where the .iss looks for files.

$publishOutput = Join-Path $root "src\XerahS.App\bin\Release\net10.0-windows10.0.26100.0"
Write-Host "Publishing to $publishOutput..."
# Ensure clean
if (Test-Path $publishOutput) { Remove-Item -Recurse -Force $publishOutput }

dotnet publish $project -c Release -p:OS=Windows_NT -r win-x64 --self-contained true -o $publishOutput

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
    dotnet publish $plugin.FullName -c Release -p:OS=Windows_NT -r win-x64 --self-contained true -o $pluginOutput

    # Cleanup: Remove files that already exist in the main app directory (deduplication)
    $pluginFiles = Get-ChildItem -Path $pluginOutput
    foreach ($file in $pluginFiles) {
        $mainAppFile = Join-Path $publishOutput $file.Name
        if (Test-Path $mainAppFile) {
            # Write-Host "Removing duplicate: $($file.Name)"
            Remove-Item $file.FullName
        }
    }
}

# 2. Compile Installer
Write-Host "Compiling Installer with Inno Setup..."
& $isccPath $issScript

# 3. Move Output
# .iss defines OutputDir={#MyAppRootDirectory}\Output
# We want to move the generated setup file to dist/
$setupFiles = Get-ChildItem -Path $innoOutputDir -Filter "*-setup.exe"
foreach ($file in $setupFiles) {
    Write-Host "Moving $($file.Name) to dist..."
    Move-Item -Path $file.FullName -Destination $outputDir -Force
}

Write-Host "Done! Installer in $outputDir"
