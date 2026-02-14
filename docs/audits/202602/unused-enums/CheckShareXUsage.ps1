
$enums = @(
    "InstallType", "PrintType", "ArrowHeadDirection", "StepType", "CutOutEffectType",
    "ShareXBuild", "CaptureType", "ScreenRecordStartMethod", "RegionCaptureType",
    "ScreenTearingTestMode", "StartupState", "BalloonTipClickAction", "NativeMessagingAction",
    "NotificationSound", "BitmapCompressionMode", "maps", "ScreenRecordOutput",
    "ScreenRecordGIFEncoding", "RegionResult", "NodePosition", "NodeShape",
    "FFmpegTune", "ShapeCategory", "ImageInsertMethod", "BorderStyle",
    "LinkFormatEnum", "OAuthLoginStatus"
)

$sharexPath = "C:\Users\liveu\source\repos\ShareX Team\ShareX"
$reportPath = "C:\Users\liveu\source\repos\ShareX Team\XerahS\docs\audits\202602\unused_enums_analysis.md"

$report = "# Unused Enums Analysis Report`n`n"
$report += "Generated on $(Get-Date)`n`n"
$report += "| Enum Name | ShareX Usage Count | Recommendation |`n"
$report += "|---|---|---|`n"

Write-Host "Checking usage in ShareX..."

foreach ($enum in $enums) {
    $count = 0
    try {
        $files = Get-ChildItem -Path $sharexPath -Recurse -Filter *.cs -ErrorAction SilentlyContinue
        # Use simple string matching first for speed, then regex if needed. 
        # But here we stick to the Audit logic: \bWord\b
        
        foreach ($file in $files) {
             $content = Get-Content $file.FullName
             $matchCount = $content | Select-String -Pattern "\b$enum\b" | Measure-Object | Select-Object -ExpandProperty Count
             $count += $matchCount
        }
    } catch {
        Write-Warning "Error checking $enum"
    }

    $rec = "Remove"
    if ($count -gt 0) {
        $rec = "Keep / Investigate (Used in ShareX)"
    }

    $report += "| **$enum** | $count | $rec |`n"
    Write-Host "$enum : $count"
}

Set-Content -Path $reportPath -Value $report
Write-Host "Report saved to $reportPath"
