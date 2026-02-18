$packageName  = 'xerahs'
$fileType     = 'exe'
$silentArgs   = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'

Uninstall-ChocolateyPackage -PackageName $packageName `
                            -FileType $fileType `
                            -SilentArgs $silentArgs
