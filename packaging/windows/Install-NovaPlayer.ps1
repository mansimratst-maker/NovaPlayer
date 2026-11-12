$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $packageRoot 'NovaPlayer'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\NovaPlayer'
if (-not (Test-Path (Join-Path $source 'NivoraPlayer.exe'))) { throw 'The Nivora Player payload is missing. Extract the complete package before installing.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenu 'Nivora Player.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $destination 'NivoraPlayer.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.IconLocation = (Join-Path $destination 'NivoraPlayer.exe') + ',0'
$shortcut.Description = 'Nivora Player — Every Frame. Every Sound. Beautifully Played.'
$shortcut.Save()
Write-Host "Nivora Player installed to $destination"
Write-Host "Launch it from the Start menu. To remove it, run Uninstall-NovaPlayer.ps1."
