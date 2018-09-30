$packageName = 'TaskSwitcher.portable'
$url = 'https://github.com/kvakulo/TaskSwitcher/releases/download/v0.9.2/TaskSwitcher-portable.zip'

Install-ChocolateyZipPackage "TaskSwitcher" "$url"  "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"