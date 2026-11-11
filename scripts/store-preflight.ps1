param([string]$Config='store/identity.json',[string]$Report='artifacts/store/preflight.json')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed,[string]$Detail) { $checks.Add([pscustomobject]@{name=$Name;passed=$Passed;detail=$Detail}) }
$configured=Test-Path -LiteralPath $Config
Check 'Partner Center configuration' $configured 'Copy store/identity.example.json to store/identity.json and supply the assigned identity and genuine public contact URLs.'
if($configured) {
 $identity=Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
 foreach($field in @('PackageName','Publisher','PublisherDisplayName','DisplayName','Version','SupportUrl','PrivacyUrl')) { Check $field (![string]::IsNullOrWhiteSpace($identity.$field)) "Required value: $field" }
 foreach($field in @('SupportUrl','PrivacyUrl')) { $uri=$null; Check ($field+' HTTPS') ([Uri]::TryCreate($identity.$field,[UriKind]::Absolute,[ref]$uri) -and $uri.Scheme -eq 'https') 'A real public HTTPS URL is required; local files are not sufficient.' }
}
$kitRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits'
$tools=@{}
foreach($name in @('makeappx.exe','signtool.exe','appcert.exe')) {
 $tool=Get-ChildItem -LiteralPath $kitRoot -Filter $name -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
 $tools[$name]=$tool.FullName
 Check $name ($null -ne $tool) $(if($tool){$tool.FullName}else{'Missing Windows SDK tool'})
}
[xml]$manifest=Get-Content src/BrainiacDesk.Package/Package.appxmanifest -Raw
Check 'Desktop only' ($manifest.Package.Dependencies.TargetDeviceFamily.Name -eq 'Windows.Desktop') 'Native WPF desktop package.'
[xml]$app=Get-Content src/BrainiacDesk.App/app.manifest -Raw
Check 'Per-monitor DPI manifest' ($app.SelectSingleNode('//*[local-name()="dpiAwareness"]').InnerText -eq 'PerMonitorV2') 'Declaration is present; physical multi-monitor DPI changes still require acceptance testing.'
Check 'No normal-operation elevation' ($app.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level -eq 'asInvoker') 'SDK/test installation may need elevation; normal application operation does not.'
Add-Type -AssemblyName System.Drawing
foreach($asset in @(@('StoreLogo.png',50),@('Square44x44Logo.png',44),@('Square150x150Logo.png',150))) {
 $path=Join-Path $root ('src/BrainiacDesk.Package/Assets/'+$asset[0]); $image=[Drawing.Image]::FromFile($path)
 try { Check $asset[0] ($image.Width -eq $asset[1] -and $image.Height -eq $asset[1]) "$($image.Width)x$($image.Height)" } finally {$image.Dispose()}
}
$reportPath=[IO.Path]::GetFullPath((Join-Path $root $Report)); New-Item -ItemType Directory -Path (Split-Path $reportPath -Parent) -Force | Out-Null
[ordered]@{utc=[DateTimeOffset]::UtcNow.ToString('o');readyToPackage=(@($checks | Where-Object {!$_.passed}).Count -eq 0);checks=$checks;tools=$tools;certification='Not certified. WACK and packaged lifecycle results must be evaluated separately.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding utf8
$checks | Format-Table name,passed,detail -AutoSize
Write-Output "Evidence: $reportPath"
