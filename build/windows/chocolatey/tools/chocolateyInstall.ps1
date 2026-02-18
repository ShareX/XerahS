$packageName  = 'xerahs'
$toolsDir     = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$url          = 'https://github.com/ShareX/XerahS/releases/latest/download/XerahS-setup.exe'
$fileType     = 'exe'
$silentArgs   = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'

Install-ChocolateyPackage -PackageName $packageName `
                          -FileType $fileType `
                          -Url $url `
                          -SilentArgs $silentArgs
