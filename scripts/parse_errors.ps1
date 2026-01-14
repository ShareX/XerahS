param ()

$ansi = [regex]'\x1b\[[0-9;]*m'
$pattern = [regex]'^(?<path>.+?)\((?<line>\d+),(?<col>\d+)\): error (?<code>\w+): (?<message>.+?) \[(?<proj>.+?)\]$'

$lines = Get-Content build.log -ErrorAction Stop
$entries = @()
$seen = @{}

foreach ($raw in $lines) {
    $line = $ansi.Replace($raw, '').Trim()
    if (-not $line) { continue }
    $match = $pattern.Match($line)
    if (-not $match.Success) { continue }

    $path = $match.Groups['path'].Value
    $lineNo = $match.Groups['line'].Value
    $colNo = $match.Groups['col'].Value
    $code = $match.Groups['code'].Value
    $key = "$path|$lineNo|$colNo|$code"
    if ($seen.ContainsKey($key)) { continue }
    $seen[$key] = $true

    $entries += [PSCustomObject]@{
        Path = $match.Groups['path'].Value
        Line = [int]$match.Groups['line'].Value
        Col = [int]$match.Groups['col'].Value
        Code = $match.Groups['code'].Value
        Message = $match.Groups['message'].Value
        Project = Split-Path $match.Groups['proj'].Value -Leaf
    }
}

$summary = $entries | Group-Object -Property Code | Sort-Object Count -Descending

$md = @()
$md += '# Build Errors Report'
$md += ''
$md += '## Error Summary'
$md += ''
$md += '| Code | Count | Projects | Files |'
$md += '| --- | --- | --- | --- |'

foreach ($group in $summary) {
    $projects = ($group.Group | Select-Object -ExpandProperty Project | Sort-Object -Unique) -join ', '
    $files = ($group.Group | Select-Object -ExpandProperty Path | ForEach-Object { Split-Path $_ -Leaf } | Sort-Object -Unique) -join ', '
    $md += "| $($group.Name) | $($group.Count) | $projects | $files |"
}

$md += ''
$md += '## Error Details'
$md += ''
$md += '| File | Line | Project | Code | Message |'
$md += '| --- | --- | --- | --- | --- |'

foreach ($entry in $entries) {
    $md += "| $($entry.Path) | $($entry.Line) | $($entry.Project) | $($entry.Code) | $($entry.Message) |"
}

Set-Content -Path docs/build/errors.md -Value ($md -join "`n") -Encoding utf8
