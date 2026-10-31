$ErrorActionPreference = 'Stop'

# Load only the function under test; running the sync script itself would mutate
# the real package files and contact GitHub before we install the request double.
$scriptPath = Join-Path $PSScriptRoot 'Sync-ChocolateyPackage.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The Chocolatey sync script did not parse.' }
$function = $ast.Find({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-ReleaseMetadata'
}, $true)
if ($null -eq $function) { throw 'Get-ReleaseMetadata was not found.' }
. ([scriptblock]::Create($function.Extent.Text))

function Invoke-RestMethod {
    param([string]$Uri, [hashtable]$Headers)
    $script:requestUri = $Uri
    $script:requestHeaders = $Headers
    return [pscustomobject]@{ tag_name = 'v1.2.3' }
}

$originalGhToken = $env:GH_TOKEN
$originalGitHubToken = $env:GITHUB_TOKEN
try {
    $cases = @(
        @{ Gh = 'fixture-gh'; GitHub = 'fixture-github'; Expected = 'Bearer fixture-gh' },
        @{ Gh = ''; GitHub = 'fixture-github'; Expected = 'Bearer fixture-github' },
        @{ Gh = '  '; GitHub = ' fixture-github '; Expected = 'Bearer fixture-github' },
        @{ Gh = ''; GitHub = ''; Expected = $null }
    )
    foreach ($case in $cases) {
        $env:GH_TOKEN = $case.Gh
        $env:GITHUB_TOKEN = $case.GitHub
        $release = Get-ReleaseMetadata -Owner 'KovaForge' -Name 'XerahS' -ReleaseVersion '1.2.3'
        if ($script:requestUri -ne 'https://api.github.com/repos/KovaForge/XerahS/releases/tags/v1.2.3') {
            throw 'Metadata request did not target the selected repository and version.'
        }
        if ($script:requestHeaders['Authorization'] -ne $case.Expected) {
            throw 'Metadata request did not use the expected authentication policy.'
        }
        if ($script:requestHeaders['User-Agent'] -ne 'XerahS-Chocolatey-Sync') { throw 'Missing GitHub User-Agent.' }
        if ($release.tag_name -ne 'v1.2.3') { throw 'Metadata response was not returned.' }
    }
    Write-Host 'Chocolatey release metadata regressions passed (4 cases).'
} finally {
    $env:GH_TOKEN = $originalGhToken
    $env:GITHUB_TOKEN = $originalGitHubToken
}
