param([Parameter(Mandatory)][string]$Package,[string]$Output='artifacts/store/wack',[switch]$RunWack)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$packagePath=(Resolve-Path -LiteralPath $Package).Path
$out=[IO.Path]::GetFullPath((Join-Path $root $Output)); New-Item -ItemType Directory -Path $out -Force | Out-Null
$signature=Get-AuthenticodeSignature -LiteralPath $packagePath
$report=[ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');package=$packagePath;sha256=(Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash;signatureStatus=[string]$signature.Status;wack='Not run';report=$null;installation='Not run';update='Not run';uninstall='Not run'}
try {
 if($RunWack) {
  $appcert=Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits') -Filter appcert.exe -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
  if(!$appcert) { throw 'Windows App Certification Kit is missing.' }
  if([IO.Path]::GetFileName($packagePath) -match 'LAYOUT-ONLY|NOT-FOR-SUBMISSION') { throw 'WACK release validation requires the final real-identity package, not the layout-only artifact.' }
  $signTool=Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue | Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending | Select-Object -First 1
  if(!$signTool) {throw 'SignTool is missing.'}
  & $signTool.FullName verify /pa $packagePath *> (Join-Path $out 'signature-verification.log')
  $report.signatureExitCode=$LASTEXITCODE
  if($LASTEXITCODE -ne 0) { throw 'Local WACK installation needs a trusted signed package. Inspect signature-verification.log. Store submission signing and local test signing are distinct.' }
  $xml=Join-Path $out 'wack-report.xml'
  & $appcert.FullName test -appxpackagepath $packagePath -reportoutputpath $xml
  $report.wackExitCode=$LASTEXITCODE; $report.report=$xml
  if(!(Test-Path -LiteralPath $xml)) { $report.wack='Failed: no report'; throw 'WACK did not produce a report.' }
  $report.wack='Report produced: review all test outcomes before approval'
  if($LASTEXITCODE -ne 0) { throw 'WACK reported failure; inspect the report.' }
 }
} finally { $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'evidence.json') -Encoding utf8 }
Write-Output 'No install/update/uninstall success is inferred from package inspection. Complete store/ACCEPTANCE.md on the final identity.'
