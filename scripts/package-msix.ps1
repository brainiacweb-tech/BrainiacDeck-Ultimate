param([string]$Config='store/identity.json',[string]$Dotnet='', [string]$MakeAppx='', [string]$Output='artifacts/store',[switch]$LayoutValidationOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
if($LayoutValidationOnly) {
 [xml]$source=Get-Content src/BrainiacDesk.Package/Package.appxmanifest -Raw
 $identity=[pscustomobject]@{PackageName=[string]$source.Package.Identity.Name;Publisher=[string]$source.Package.Identity.Publisher;Version=[string]$source.Package.Identity.Version;DisplayName=[string]$source.Package.Properties.DisplayName;PublisherDisplayName=[string]$source.Package.Properties.PublisherDisplayName}
 Write-Warning 'LAYOUT VALIDATION ONLY: source identity retained. This package must never be submitted or installed as a release.'
} else {
 if(!(Test-Path -LiteralPath $Config)) { throw 'Real Partner Center identity is required. Copy store/identity.example.json to store/identity.json; no fabricated submission identity is used.' }
 $identity=Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
 foreach($field in @('PackageName','Publisher','PublisherDisplayName','DisplayName','Version')) { if([string]::IsNullOrWhiteSpace($identity.$field) -or $identity.$field -match 'REPLACE|PLACEHOLDER') { throw "Missing real identity field: $field" } }
}
if($identity.PackageName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') { throw 'Invalid assigned package name.' }
if($identity.Publisher -notmatch '^CN=') { throw 'Publisher must be the exact X.500 identity from Partner Center.' }
$version=$null
if(![Version]::TryParse($identity.Version,[ref]$version) -or $version.Build -lt 0 -or $version.Revision -ne 0 -or $version.Major -lt 1 -and $version.Minor -lt 1 -or @($version.Major,$version.Minor,$version.Build,$version.Revision | Where-Object {$_ -gt 65535}).Count) { throw 'Use a valid four-part Store version with revision 0 and nonzero major/minor.' }
if(!$Dotnet) { $local=Join-Path $env:LOCALAPPDATA 'BrainiacDesk/dotnet/dotnet.exe'; $Dotnet=if(Test-Path -LiteralPath $local){$local}else{'dotnet'} }
if(!$MakeAppx) { $tool=Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10') -Filter makeappx.exe -File -Recurse -ErrorAction SilentlyContinue | Where-Object {$_.Directory.Name -eq 'x64' -or $_.Directory.Name -eq 'App Certification Kit'} | Sort-Object FullName -Descending | Select-Object -First 1; if(!$tool){throw 'Install Windows SDK MakeAppx before packaging.'}; $MakeAppx=$tool.FullName }
$out=[IO.Path]::GetFullPath((Join-Path $root $Output)); New-Item -ItemType Directory -Path $out -Force | Out-Null
$payload=Join-Path $out ('payload-'+[Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $payload | Out-Null
& $Dotnet publish src/BrainiacDesk.App -c Release -r win-x64 --self-contained true -o $payload
if($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
[xml]$manifest=Get-Content src/BrainiacDesk.Package/Package.appxmanifest -Raw
$manifest.Package.Identity.SetAttribute('Name',$identity.PackageName); $manifest.Package.Identity.SetAttribute('Publisher',$identity.Publisher); $manifest.Package.Identity.SetAttribute('Version',$identity.Version)
$manifest.Package.Properties.DisplayName=$identity.DisplayName; $manifest.Package.Properties.PublisherDisplayName=$identity.PublisherDisplayName
$visual=$manifest.Package.Applications.Application.VisualElements; $visual.SetAttribute('DisplayName',$identity.DisplayName)
$manifest.Save((Join-Path $payload 'AppxManifest.xml'))
Copy-Item -LiteralPath src/BrainiacDesk.Package/Assets -Destination $payload -Recurse -Force
Copy-Item -LiteralPath PRIVACY.md,LICENSE,docs/HELP.md -Destination $payload -Force
$fileName=if($LayoutValidationOnly){'LAYOUT-ONLY-NOT-FOR-SUBMISSION.msix'}else{$identity.PackageName+'-'+$identity.Version+'-x64.msix'}
$package=Join-Path $out $fileName
& $MakeAppx pack /d $payload /p $package /o *> (Join-Path $out 'makeappx-pack.log')
if($LASTEXITCODE -ne 0) { throw 'MakeAppx schema/package validation failed.' }
$unpack=Join-Path $out ('validated-'+[Guid]::NewGuid().ToString('N'))
& $MakeAppx unpack /p $package /d $unpack /o *> (Join-Path $out 'makeappx-unpack.log')
if($LASTEXITCODE -ne 0) { throw 'MakeAppx unpack validation failed.' }
$hash=Get-FileHash -LiteralPath $package -Algorithm SHA256
[ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');package=$package;sha256=$hash.Hash;identity=$identity.PackageName;publisher=$identity.Publisher;version=$identity.Version;makeappxValidated=$true;submissionEligibleIdentity=(!$LayoutValidationOnly);signed=$false;wack='Not run';installedLifecycle='Not run'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'package-evidence.json') -Encoding utf8
if($LayoutValidationOnly) { Write-Output "Layout validation artifact ONLY: $package. Real identity, installation and WACK are still pending." } else { Write-Output "Validated unsigned Store MSIX: $package. Store signs accepted submissions; local installation tests need a trusted matching certificate." }
