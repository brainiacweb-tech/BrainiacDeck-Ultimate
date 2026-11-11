param([string]$Dotnet='', [switch]$Interactive, [string]$Output='artifacts/realworld-test-build')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
if(!$Dotnet) { $localSdk=Join-Path $env:LOCALAPPDATA 'BrainiacDesk/dotnet/dotnet.exe'; $Dotnet=if(Test-Path -LiteralPath $localSdk) {$localSdk} else {'dotnet'} }
$evidence=Join-Path $root 'artifacts/release-verification'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$checks=[Collections.Generic.List[object]]::new()
$verificationSucceeded=$false
function Run-Check([string]$Name,[string[]]$Arguments) {
 $log=Join-Path $evidence ($Name+'.log')
 & $Dotnet @Arguments *> $log
 $code=$LASTEXITCODE
 $checks.Add([ordered]@{name=$Name;exitCode=$code;log=$log})
 Get-Content -LiteralPath $log -Tail 5
 if($code -ne 0) { throw "$Name failed. See $log" }
}
try {
 Run-Check 'release-build' @('build','BrainiacDesk.slnx','-c','Release')
 Run-Check 'core-checks' @('run','--project','tests/BrainiacDesk.Tests','-c','Release','--no-build')
 Run-Check 'windows-checks' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build')
 if($Interactive) { Run-Check 'fresh-install' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--fresh-install-tests'); Run-Check 'store-ui-review' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--store-review'); Run-Check 'single-window-navigation' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--navigation-tests'); Run-Check 'redesign-ui-integration' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--design-tests'); Run-Check 'edge-ui-integration' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--edge-tests'); Run-Check 'overlay-ui-workflows' @('run','--project','tests/BrainiacDesk.WindowsTests','-c','Release','--no-build','--','--teleprompter-tests') }
 Run-Check 'vulnerability-audit' @('list','src/BrainiacDesk.App','package','--vulnerable','--include-transitive','--no-restore','--format','json')
 $raw=Get-Content (Join-Path $evidence 'vulnerability-audit.log') -Raw
 $audit=$raw.Substring($raw.IndexOf('{')) | ConvertFrom-Json
 $vulnerable=@($audit.projects.frameworks.topLevelPackages | Where-Object {$_.vulnerabilities})+@($audit.projects.frameworks.transitivePackages | Where-Object {$_.vulnerabilities})
 if($vulnerable.Count) { throw 'Reported vulnerable packages require review.' }
 Run-Check 'self-contained-publish' @('publish','src/BrainiacDesk.App','-c','Release','-r','win-x64','--self-contained','true','-o',$Output)
 if($Interactive) {
  try { & (Join-Path $PSScriptRoot 'ui-smoke.ps1') -Executable (Join-Path $Output 'BrainiacDesk.App.exe') *> (Join-Path $evidence 'portable-ui-smoke.log') }
  catch { $checks.Add([ordered]@{name='portable-ui-smoke';exitCode=1;log=(Join-Path $evidence 'portable-ui-smoke.log');error=$_.Exception.Message}); throw }
  $checks.Add([ordered]@{name='portable-ui-smoke';exitCode=0;log=(Join-Path $evidence 'portable-ui-smoke.log')})
  Get-Content (Join-Path $evidence 'portable-ui-smoke.log') -Tail 5
 }
 Copy-Item -LiteralPath PRIVACY.md,LICENSE -Destination $Output -Force
 Copy-Item -LiteralPath docs/PRODUCTION-AUDIT.md,docs/HELP.md -Destination $Output -Force
 Get-FileHash (Join-Path $Output 'BrainiacDesk.App.exe') -Algorithm SHA256 | ConvertTo-Json | Set-Content (Join-Path $evidence 'executable-hash.json')
 $verificationSucceeded=$true
} finally {
 $manifest=Get-Content src/BrainiacDesk.Package/Package.appxmanifest -Raw
 $sdk=Get-Command makeappx.exe,signtool.exe,appcert.exe -ErrorAction SilentlyContinue | Select-Object Name,Source
 [ordered]@{succeeded=$verificationSucceeded;utc=[DateTimeOffset]::UtcNow.ToString('o');checks=$checks;interactive=[bool]$Interactive;output=$Output;storeIdentityConfigured=(!$manifest.Contains('REPLACE-WITH'));windowsSdkCommands=@($sdk);manualAcceptance='See docs/PRODUCTION-AUDIT.md; no Store certification is claimed.'} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'results.json')
}