$projects = @(
    "XerahS.CLI",
    "XerahS.Common",
    "XerahS.Core",
    "XerahS.UI",
    "XerahS.Platform.Windows",
    "XerahS.Platform.Linux",
    "XerahS.Platform.MacOS",
    "XerahS.Platform.Abstractions",
    "XerahS.Bootstrap",
    "XerahS.Uploaders",
    "XerahS.Services",
    "XerahS.ViewModels",
    "XerahS.Media",
    "XerahS.History",
    "XerahS.Indexer",
    "XerahS.PluginExporter",
    "ShareX.Imgur.Plugin",
    "ShareX.AmazonS3.Plugin"
)

Write-Host "=== MISSING FILES BY PROJECT ==="
$missingFiles = Get-Content "missing_files.txt"
foreach ($project in $projects) {
    $count = ($missingFiles | Where-Object { $_ -match [regex]::Escape($project) }).Count
    if ($count -gt 0) {
        Write-Host "$project : $count files"
    }
}

Write-Host "`n=== INCORRECT FILES BY PROJECT ==="
$incorrectFiles = Get-Content "incorrect_files.txt"
foreach ($project in $projects) {
    $count = ($incorrectFiles | Where-Object { $_ -match [regex]::Escape($project) }).Count
    if ($count -gt 0) {
        Write-Host "$project : $count files"
    }
}

Write-Host "`n=== MISPLACED FILES BY PROJECT ==="
$misplacedFiles = Get-Content "misplaced_files.txt"
foreach ($project in $projects) {
    $count = ($misplacedFiles | Where-Object { $_ -match [regex]::Escape($project) }).Count
    if ($count -gt 0) {
        Write-Host "$project : $count files"
    }
}
