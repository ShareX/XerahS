[CmdletBinding()]
param(
    [string]$Version,
    [string]$RepositoryOwner,
    [string]$RepositoryName,
    [string]$Repository,
    [switch]$Pack,
    [switch]$Push,
    [string]$OutputDirectory,
    [string]$PushSource = 'https://push.chocolatey.org/',
    [string]$ApiKey,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
} else {
    $scriptRoot = $PSScriptRoot
}

$packageRoot = (Resolve-Path $scriptRoot).Path
$repoRoot = (Resolve-Path (Join-Path $packageRoot '..\..\..')).Path
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$nuspecPath = Join-Path $packageRoot 'xerahs.nuspec'
$installScriptPath = Join-Path $packageRoot 'tools\chocolateyInstall.ps1'
$verificationPath = Join-Path $packageRoot 'tools\VERIFICATION.txt'

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    } finally {
        $writer.Dispose()
    }
}

function Get-RepositoryVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $props = [xml](Get-Content -Path $Path)
    $versionNode = $props.SelectSingleNode('//Version')
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Could not resolve <Version> from $Path."
    }

    return $versionNode.InnerText.Trim()
}

function Resolve-GitHubRepository {
    param(
        [string]$Owner,
        [string]$Name,
        [string]$RepositoryFullName,
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    if (-not [string]::IsNullOrWhiteSpace($Owner) -and -not [string]::IsNullOrWhiteSpace($Name)) {
        return @{
            Owner = $Owner.Trim()
            Name = $Name.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($RepositoryFullName)) {
        $RepositoryFullName = $env:GITHUB_REPOSITORY
    }

    if (-not [string]::IsNullOrWhiteSpace($RepositoryFullName) -and $RepositoryFullName -match '^(?<owner>[^/]+)/(?<name>[^/]+)$') {
        return @{
            Owner = $Matches.owner
            Name = $Matches.name
        }
    }

    $originUrl = $null
    try {
        $originUrl = & git -C $Root remote get-url origin 2>$null
    } catch {
        $originUrl = $null
    }

    if (-not [string]::IsNullOrWhiteSpace($originUrl)) {
        $originUrl = $originUrl.Trim()
        # Support github.com and KovaForge per-person SSH aliases:
        # git@github-vladislava:KovaForge/XerahS.git
        if ($originUrl -match '(?:github\.com|github-[A-Za-z0-9_-]+)[:/](?<owner>[^/]+)/(?<name>[^/.]+?)(?:\.git)?/?$') {
            return @{
                Owner = $Matches.owner
                Name = $Matches.name
            }
        }
    }

    throw "Could not resolve GitHub repository. Pass -Repository owner/name or -RepositoryOwner and -RepositoryName."
}

function Get-ReleaseMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Owner,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    $tag = "v$ReleaseVersion"
    $uri = "https://api.github.com/repos/$Owner/$Name/releases/tags/$tag"

    $headers = @{
        'User-Agent' = 'XerahS-Chocolatey-Sync'
        'Accept' = 'application/vnd.github+json'
    }
    # Hosted runners share anonymous API quotas. Use the job token when supplied;
    # retain anonymous access for manual runs that do not have a GitHub token.
    $apiToken = $env:GH_TOKEN
    if ([string]::IsNullOrWhiteSpace($apiToken)) {
        $apiToken = $env:GITHUB_TOKEN
    }
    if (-not [string]::IsNullOrWhiteSpace($apiToken)) {
        $headers['Authorization'] = 'Bearer ' + $apiToken.Trim()
    }

    try {
        return Invoke-RestMethod -Uri $uri -Headers $headers
    } catch {
        throw "Failed to fetch GitHub release metadata for $tag from $uri. $($_.Exception.Message)"
    }
}

function Get-RequiredAsset {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Assets,
        [Parameter(Mandatory = $true)]
        [string]$AssetName
    )

    $asset = $Assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
    if ($null -eq $asset) {
        throw "Required release asset was not found: $AssetName"
    }

    return $asset
}

function Get-Sha256Digest {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Asset
    )

    if (-not [string]::IsNullOrWhiteSpace($Asset.digest) -and $Asset.digest.StartsWith('sha256:')) {
        return $Asset.digest.Substring(7).ToLowerInvariant()
    }

    if ([string]::IsNullOrWhiteSpace($Asset.browser_download_url)) {
        throw "Release asset digest is missing and browser_download_url is unavailable for $($Asset.name)."
    }

    Write-Warning "Release asset digest was unavailable for $($Asset.name); downloading asset to calculate SHA256."

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('xerahs-choco-' + [Guid]::NewGuid().ToString('N'))
    $tempAssetPath = Join-Path $tempRoot $Asset.name

    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    try {
        Invoke-WebRequest -Uri $Asset.browser_download_url `
                          -Headers @{ 'User-Agent' = 'XerahS-Chocolatey-Sync' } `
                          -OutFile $tempAssetPath

        return (Get-FileHash -Path $tempAssetPath -Algorithm SHA256).Hash.ToLowerInvariant()
    } finally {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Set-NuspecMetadataValue {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNamespaceManager]$NamespaceManager,
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement]$MetadataNode,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $node = $MetadataNode.SelectSingleNode("ns:$Name", $NamespaceManager)
    if ($null -eq $node) {
        $node = $Document.CreateElement($Name, $Document.DocumentElement.NamespaceURI)
        [void]$MetadataNode.AppendChild($node)
    }

    $node.InnerText = $Value
}

function Replace-InstallScriptPattern {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Replacement
    )

    $options = [System.Text.RegularExpressions.RegexOptions]::Multiline
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($Content, $Pattern, $options)) {
        throw "Failed to update install script pattern: $Pattern"
    }

    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $Content,
        $Pattern,
        $Replacement,
        $options
    )

    return $updated
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-RepositoryVersion -Path $propsPath
    Write-Host "Auto-detected version: $Version"
}

if ($Push -and -not $Pack) {
    throw "Use -Pack together with -Push."
}

if ($Push -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "An API key is required when using -Push."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'dist\chocolatey'
}

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$resolvedRepository = Resolve-GitHubRepository -Owner $RepositoryOwner -Name $RepositoryName -RepositoryFullName $Repository -Root $repoRoot
$RepositoryOwner = $resolvedRepository.Owner
$RepositoryName = $resolvedRepository.Name
Write-Host "GitHub release repository: $RepositoryOwner/$RepositoryName"

$release = Get-ReleaseMetadata -Owner $RepositoryOwner -Name $RepositoryName -ReleaseVersion $Version
$tag = "v$Version"
$websiteUrl = 'https://xerahs.com/'
$releaseUrl = $release.html_url
$repoUrl = "https://github.com/$RepositoryOwner/$RepositoryName"
$projectSourceUrl = $repoUrl
$bugTrackerUrl = "$repoUrl/issues"
$packageSourceUrl = "$repoUrl/tree/$tag/build/windows/chocolatey"
$x64AssetName = "XerahS-$Version-win-x64.exe"
$arm64AssetName = "XerahS-$Version-win-arm64.exe"
$x64Asset = Get-RequiredAsset -Assets $release.assets -AssetName $x64AssetName
$arm64Asset = Get-RequiredAsset -Assets $release.assets -AssetName $arm64AssetName
$x64Checksum = Get-Sha256Digest -Asset $x64Asset
$arm64Checksum = Get-Sha256Digest -Asset $arm64Asset
$currentYear = if ($null -ne $release.published_at) {
    ([DateTime]$release.published_at).Year
} else {
    [DateTime]::UtcNow.Year
}

$nuspec = [xml](Get-Content -Path $nuspecPath)
$namespaceManager = New-Object System.Xml.XmlNamespaceManager($nuspec.NameTable)
$namespaceManager.AddNamespace('ns', $nuspec.DocumentElement.NamespaceURI)
$metadataNode = $nuspec.SelectSingleNode('/ns:package/ns:metadata', $namespaceManager)
if ($null -eq $metadataNode) {
    throw "The nuspec metadata node was not found in $nuspecPath."
}

Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'version' -Value $Version
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'projectUrl' -Value $websiteUrl
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'projectSourceUrl' -Value $projectSourceUrl
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'bugTrackerUrl' -Value $bugTrackerUrl
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'packageSourceUrl' -Value $packageSourceUrl
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'licenseUrl' -Value 'https://www.gnu.org/licenses/gpl-3.0-standalone.html'
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'iconUrl' -Value 'https://xerahs.com/assets/Logo.png'
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'releaseNotes' -Value $releaseUrl
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'copyright' -Value "Copyright (c) 2007-$currentYear ShareX Team"
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'tags' -Value 'xerahs sharex screenshot capture file-sharing avalonia'
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'summary' -Value 'Cross-platform screen capture and sharing tool built with Avalonia UI.'
Set-NuspecMetadataValue -Document $nuspec -NamespaceManager $namespaceManager -MetadataNode $metadataNode -Name 'description' -Value 'XerahS is a cross-platform ShareX-compatible screen capture and file sharing tool built with Avalonia UI and .NET 10.'

$installScriptContent = Get-Content -Path $installScriptPath -Raw
$installScriptContent = Replace-InstallScriptPattern -Content $installScriptContent -Pattern '^\$repository\s*=\s*''[^'']*''\r?$' -Replacement ('$repository = ''{0}/{1}''' -f $RepositoryOwner, $RepositoryName)
$installScriptContent = Replace-InstallScriptPattern -Content $installScriptContent -Pattern '^\$x64Checksum\s*=\s*''[^'']*''\r?$' -Replacement ('$x64Checksum  = ''{0}''' -f $x64Checksum)
$installScriptContent = Replace-InstallScriptPattern -Content $installScriptContent -Pattern '^\$arm64Checksum\s*=\s*''[^'']*''\r?$' -Replacement ('$arm64Checksum = ''{0}''' -f $arm64Checksum)

$verificationContent = @"
VERIFICATION
Verification is intended for Chocolatey moderators and reviewers.

Release:
  $releaseUrl

Installers:
  x64
    URL: $($x64Asset.browser_download_url)
    SHA256: $x64Checksum

  arm64
    URL: $($arm64Asset.browser_download_url)
    SHA256: $arm64Checksum

The installer binaries are downloaded from the official XerahS GitHub release and are not redistributed inside this package.
"@

Write-Host "Prepared Chocolatey metadata for $Version"
Write-Host "  x64 checksum : $x64Checksum"
Write-Host "  arm64 checksum: $arm64Checksum"
Write-Host "  release URL   : $releaseUrl"

if (-not $DryRun) {
    Save-XmlDocument -Document $nuspec -Path $nuspecPath
    Write-Utf8NoBomFile -Path $installScriptPath -Content $installScriptContent
    Write-Utf8NoBomFile -Path $verificationPath -Content ($verificationContent.TrimEnd("`r", "`n") + "`r`n")
}

$packagePath = Join-Path $OutputDirectory "xerahs.$Version.nupkg"

if ($Pack) {
    if (-not $DryRun -and -not (Test-Path $OutputDirectory)) {
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    }

    if ($DryRun) {
        Write-Host "Dry run: would pack $nuspecPath to $OutputDirectory"
    } else {
        if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
            throw "Chocolatey CLI (choco) is required to pack or push the package."
        }

        Push-Location $packageRoot
        try {
            & choco pack $nuspecPath --outputdirectory $OutputDirectory
            if ($LASTEXITCODE -ne 0) {
                throw "choco pack failed with exit code $LASTEXITCODE."
            }
        } finally {
            Pop-Location
        }

        Write-Host "Packed Chocolatey package: $packagePath"
    }
}

if ($Push) {
    if ($DryRun) {
        Write-Host "Dry run: would push $packagePath to $PushSource"
    } else {
        if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
            throw "Chocolatey CLI (choco) is required to pack or push the package."
        }

        & choco push $packagePath --source $PushSource --api-key $ApiKey
        if ($LASTEXITCODE -ne 0) {
            throw "choco push failed with exit code $LASTEXITCODE."
        }

        Write-Host "Pushed Chocolatey package: $packagePath"
    }
}
