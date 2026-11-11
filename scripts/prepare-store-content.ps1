param([string]$Config='store/identity.json',[string]$Output='artifacts/store/submission')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
if(!(Test-Path -LiteralPath $Config)) { throw 'Supply real identity and public support/privacy details in store/identity.json first.' }
$identity=Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
foreach($field in @('DisplayName','SupportUrl','PrivacyUrl')) { if([string]::IsNullOrWhiteSpace($identity.$field)) {throw "Missing $field"} }
$email=$null; if($identity.SupportEmail) { try {$email=[System.Net.Mail.MailAddress]::new($identity.SupportEmail)} catch {throw 'Invalid support email'} }
foreach($field in @('SupportUrl','PrivacyUrl')) { $uri=$null; if(![Uri]::TryCreate($identity.$field,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https') { throw "Use a genuine public HTTPS URL for $field" } }
$out=[IO.Path]::GetFullPath((Join-Path $root $Output)); New-Item -ItemType Directory -Path $out -Force | Out-Null
$listing=Get-Content store/listing.en-us.json -Raw | ConvertFrom-Json
$listing.displayName=$identity.DisplayName; $listing.supportEmail=$identity.SupportEmail; $listing.supportUrl=$identity.SupportUrl; $listing.privacyUrl=$identity.PrivacyUrl
$listing.submissionStatus='Content prepared; package validation, age rating, availability and final approval remain separate.'
$listing | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'listing.en-us.json') -Encoding utf8
$html=Get-Content store/privacy.html -Raw
$html=$html -replace '<p>Before public distribution.*?</p>',''
$contact='<section><h2>Contact</h2><p>For support or privacy questions, visit the <a href="'+[Net.WebUtility]::HtmlEncode($identity.SupportUrl)+'">project support page</a>. Issues are public: do not post private scripts, screenshots, credentials or personal files.</p></section>'
if($email) { $contact+='<p>Email: <a href="mailto:'+ [Net.WebUtility]::HtmlEncode($email.Address)+'">'+[Net.WebUtility]::HtmlEncode($email.Address)+'</a>.</p>' }
$html=$html.Replace('</main>',$contact+'</main>')
$html | Set-Content -LiteralPath (Join-Path $out 'privacy.html') -Encoding utf8
Copy-Item -LiteralPath store/CERTIFICATION-NOTES.md,store/ACCEPTANCE.md -Destination $out -Force
Write-Output "Prepared $out. Publishing privacy.html to the declared public URL is a separate deployment step."
