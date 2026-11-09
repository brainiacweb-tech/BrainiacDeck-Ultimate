param([string]$Dotnet = 'dotnet', [string]$MakeAppx = 'makeappx.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$manifest = Join-Path $root 'src/BrainiacDesk.Package/Package.appxmanifest'
if ((Get-Content -LiteralPath $manifest -Raw).Contains('REPLACE-WITH')) { throw 'Set your real Partner Center publisher identity in Package.appxmanifest before packaging.' }
$payload = Join-Path $root 'artifacts/msix-payload'
& $Dotnet publish (Join-Path $root 'src/BrainiacDesk.App') -c Release -r win-x64 --self-contained true -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath $manifest -Destination (Join-Path $payload 'AppxManifest.xml')
Copy-Item -LiteralPath (Join-Path $root 'src/BrainiacDesk.Package/Assets') -Destination $payload -Recurse -Force
& $MakeAppx pack /d $payload /p (Join-Path $root 'artifacts/BrainiacDesk-0.2.0-x64.msix') /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX generation failed.' }
Write-Output 'Unsigned MSIX created. Signing, installation, upgrades, uninstall, and certification tests remain required.'
