# Comprehensive Unused Property Analysis for XerahS
# Finds properties with zero references across the codebase

param(
    [int]$SampleSize = 0  # 0 = analyze all
)

$srcPath = "c:\Users\liveu\source\repos\ShareX Team\XerahS\src"
$unusedProperties = @()
$analyzedCount = 0

Write-Host "`n=== XerahS Unused Property Analysis ===" -ForegroundColor Cyan
Write-Host "Loading property inventory..." -ForegroundColor Yellow

# Load the property inventory
$inventory = Import-Csv 'c:\Users\liveu\source\repos\ShareX Team\XerahS\docs\technical\properties_inventory.csv'
$totalProperties = $inventory.Count

if ($SampleSize -gt 0 -and $SampleSize -lt $totalProperties) {
    $inventory = $inventory | Get-Random -Count $SampleSize
    Write-Host "Analyzing random sample of $SampleSize properties" -ForegroundColor Yellow
} else {
    Write-Host "Analyzing all $totalProperties properties" -ForegroundColor Yellow
}

# Group by file for efficient processing
$groupedByFile = $inventory | Group-Object -Property File

Write-Host "Searching for usage patterns..." -ForegroundColor Yellow
Write-Host "This will take several minutes...`n" -ForegroundColor Yellow

foreach ($fileGroup in $groupedByFile) {
    $file = $fileGroup.Name
    $relativePath = $fileGroup.Group[0].RelativePath
    
    foreach ($prop in $fileGroup.Group) {
        $analyzedCount++
        $percentage = [math]::Round(($analyzedCount / $inventory.Count) * 100, 1)
        
        Write-Progress -Activity "Analyzing property usage" `
            -Status "$percentage% - Checking: $($prop.PropertyName) in $([System.IO.Path]::GetFileName($file))" `
            -PercentComplete $percentage
        
        $propertyName = $prop.PropertyName
        
        # Search for usage of this property across all .cs files
        # Exclude the definition line itself
        $searchPattern = "\b$propertyName\b"
        
        try {
            $usages = Select-String -Path "$srcPath\**\*.cs" -Pattern $searchPattern -ErrorAction SilentlyContinue
            
            # Filter out the definition itself and XML comments
            $actualUsages = $usages | Where-Object {
                $line = $_.Line.Trim()
                
                # Skip the property definition line
                if ($line -match "^\s*(public|private|protected|internal).*\s+$propertyName\s*\{") {
                    return $false
                }
                
                # Skip XML comments
                if ($line -match "^///") {
                    return $false
                }
                
                # Skip blank lines or lines that are just braces
                if ($line -match "^[\{\}]?\s*$") {
                    return $false
                }
                
                return $true
            }
            
            $usageCount = ($actualUsages | Measure-Object).Count
            
            # If zero usages found (excluding definition), mark as unused
            if ($usageCount -eq 0) {
                $unusedProperties += [PSCustomObject]@{
                    PropertyName = $propertyName
                    PropertyType = $prop.PropertyType
                    AccessModifier = $prop.AccessModifier
                    File = [System.IO.Path]::GetFileName($file)
                    RelativePath = $relativePath
                    Namespace = $prop.Namespace
                    LineNumber = $prop.LineNumber
                    Project = ($relativePath -split '\\')[0]
                }
            }
        }
        catch {
            Write-Warning "Error analyzing $propertyName : $_"
        }
    }
}

Write-Progress -Activity "Analyzing property usage" -Completed

Write-Host "`n=== Analysis Complete ===" -ForegroundColor Green
Write-Host "Total properties analyzed: $analyzedCount" -ForegroundColor Cyan
Write-Host "Unused properties found: $($unusedProperties.Count)" -ForegroundColor $(if ($unusedProperties.Count -gt 0) { "Yellow" } else { "Green" })

if ($unusedProperties.Count -gt 0) {
    # Group by project
    $byProject = $unusedProperties | Group-Object -Property Project | Sort-Object Name
    
    Write-Host "`n=== Unused Properties by Project ===" -ForegroundColor Cyan
    foreach ($projectGroup in $byProject) {
        Write-Host "`n[$($projectGroup.Name)] - $($projectGroup.Count) unused properties" -ForegroundColor Yellow
        $projectGroup.Group | Format-Table PropertyName, PropertyType, AccessModifier, File, LineNumber -AutoSize | Out-String | Write-Host
    }
    
    # Export detailed report
    $reportPath = "c:\Users\liveu\source\repos\ShareX Team\XerahS\docs\technical\unused_properties_report.csv"
    $unusedProperties | Export-Csv -Path $reportPath -NoTypeInformation
    Write-Host "Detailed report saved to: $reportPath" -ForegroundColor Green
}

Write-Host "`nAnalysis complete!" -ForegroundColor Cyan
