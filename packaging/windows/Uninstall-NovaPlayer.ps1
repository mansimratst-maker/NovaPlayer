$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\NovaPlayer'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\NovaPlayer.lnk'
Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
if (Test-Path $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
Write-Host 'NovaPlayer was uninstalled. Your library and settings were kept in LocalAppData.'
