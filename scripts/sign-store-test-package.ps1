param([Parameter(Mandatory)][string]$Package,[Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$Thumbprint,[string]$Output='artifacts/store/signed-test.msix')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$source=(Resolve-Path -LiteralPath $Package).Path
if([IO.Path]::GetFileName($source) -match 'LAYOUT-ONLY|NOT-FOR-SUBMISSION') {throw 'Do not sign placeholder-identity layout artifacts.'}
$certificate=Get-Item -LiteralPath ('Cert:/CurrentUser/My/'+$Thumbprint)
if(!$certificate.HasPrivateKey -or $certificate.NotAfter -lt [DateTime]::Now) {throw 'An unexpired certificate with a private key is required.'}
$zip=[IO.Compression.ZipFile]::OpenRead($source)
try { $entry=$zip.GetEntry('AppxManifest.xml'); if(!$entry){throw 'Missing manifest'}; $reader=[IO.StreamReader]::new($entry.Open()); try {[xml]$manifest=$reader.ReadToEnd()} finally {$reader.Dispose()} } finally {$zip.Dispose()}
if($certificate.Subject -ne $manifest.Package.Identity.Publisher) {throw 'Certificate subject must exactly match the package publisher.'}
$target=[IO.Path]::GetFullPath((Join-Path $root $Output)); if($target -eq $source){throw 'Keep the unsigned Store package separate from local test signing.'}
New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $target -Force
$tool=Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') -Filter signtool.exe -Recurse -File | Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending | Select-Object -First 1
if(!$tool){throw 'SignTool is missing.'}
& $tool.FullName sign /fd SHA256 /s My /sha1 $Thumbprint $target
if($LASTEXITCODE -ne 0){throw 'Test package signing failed.'}
& $tool.FullName verify /pa $target
if($LASTEXITCODE -ne 0){throw 'Test signature is not trusted on this test device. Do not bypass trust validation.'}
Write-Output "Signed and verified local test copy: $target. Certificate trust and packaged lifecycle must be managed on your test device; private keys were not exported."
