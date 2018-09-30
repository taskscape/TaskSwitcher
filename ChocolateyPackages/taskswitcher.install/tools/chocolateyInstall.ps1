$packageName = 'TaskSwitcher.install'
$installerType = 'EXE'
$url = 'https://github.com/kvakulo/TaskSwitcher/releases/download/v0.9.2/TaskSwitcher-setup.exe'
$silentArgs = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
$validExitCodes = @(0)

Install-ChocolateyPackage "$packageName" "$installerType" "$silentArgs" "$url" "$url64"  -validExitCodes $validExitCodes