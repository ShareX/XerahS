# Analyze Unused Properties in XerahS
# This script finds all properties with zero references

$srcPath = "c:\Users\liveu\source\repos\ShareX Team\XerahS\src"
$results = @()

Write-Host "Scanning for .cs files..." -ForegroundColor Cyan

# Get all .cs files excluding generated files and obj/bin directories
$csFiles = Get-ChildItem -Path $srcPath -Filter "*.cs" -Recurse | 
    Where-Object { 
        $_.FullName -notmatch '\\obj\\' -and 
        $_.FullName -notmatch '\\bin\\' -and
        $_.Name -notmatch '\.(Designer|g|g\.i)\.cs$'
    }

Write-Host "Found $($csFiles.Count) C# files to analyze" -ForegroundColor Green
Write-Host "Extracting properties..." -ForegroundColor Cyan

$totalFiles = $csFiles.Count
$currentFile = 0

foreach ($file in $csFiles) {
    $currentFile++
    $percentage = [math]::Round(($currentFile / $totalFiles) * 100, 1)
    Write-Progress -Activity "Analyzing files" -Status "$percentage% - $($file.Name)" -PercentComplete $percentage
    
    $content = Get-Content $file.FullName -Raw
    if (-not $content) { continue }
    
    # Extract namespace
    $namespace = ""
    if ($content -match 'namespace\s+([\w\.]+)') {
        $namespace = $Matches[1]
    }
    
    # Find all property declarations (various patterns)
    # Pattern 1: Auto-properties with get; set;
    $pattern1 = '(?m)^\s*(public|private|protected|internal|public\s+override|protected\s+override|public\s+virtual|protected\s+virtual)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[^>]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*\{\s*get\s*;\s*(?:set\s*;|init\s*;)?\s*\}'
    
    # Pattern 2: Expression-bodied properties
    $pattern2 = '(?m)^\s*(public|private|protected|internal|public\s+override|protected\s+override)\s+(?:static\s+)?(\w+(?:<[^>]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*=>\s*'
    
    # Pattern 3: Properties with body
    $pattern3 = '(?m)^\s*(public|private|protected|internal|public\s+override|protected\s+override)\s+(?:static\s+)?(\w+(?:<[^>]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*\{\s*get\s*\{'
    
    foreach ($pattern in @($pattern1, $pattern2, $pattern3)) {
        $matches = [regex]::Matches($content, $pattern)
        
        foreach ($match in $matches) {
            $modifier = $match.Groups[1].Value.Trim()
            $type = $match.Groups[2].Value.Trim()
            $name = $match.Groups[3].Value.Trim()
            
            # Skip backing fields (usually start with underscore or lowercase)
            if ($name -match '^_' -or ($name -cmatch '^[a-z]' -and $modifier -eq 'private')) {
                continue
            }
            
            # Calculate line number
            $lineNumber = ($content.Substring(0, $match.Index) -split "`n").Count
            
            $results += [PSCustomObject]@{
                File = $file.FullName
                RelativePath = $file.FullName.Replace($srcPath + '\', '')
                Namespace = $namespace
                PropertyName = $name
                PropertyType = $type
                AccessModifier = $modifier
                LineNumber = $lineNumber
            }
        }
    }
}

Write-Progress -Activity "Analyzing files" -Completed

Write-Host "`nFound $($results.Count) properties" -ForegroundColor Green
Write-Host "Exporting to CSV for reference checking..." -ForegroundColor Cyan

# Export for analysis
$outputPath = "c:\Users\liveu\source\repos\ShareX Team\XerahS\docs\technical\properties_inventory.csv"
$results | Export-Csv -Path $outputPath -NoTypeInformation

Write-Host "Exported to: $outputPath" -ForegroundColor Green
Write-Host "`nAnalysis complete!" -ForegroundColor Cyan
