$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$dotnet = Join-Path $env:LOCALAPPDATA 'BrainiacDesk/dotnet/dotnet.exe'
$dll = Join-Path $PSScriptRoot '../src/BrainiacDesk.App/bin/Release/net10.0-windows/BrainiacDesk.App.dll'
$process = Start-Process -FilePath $dotnet -ArgumentList @('exec', ('"' + $dll + '"')) -WindowStyle Hidden -PassThru
try {
    $window = $null
    for ($attempt = 0; $attempt -lt 160; $attempt++) {
        Start-Sleep -Milliseconds 250
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id))
        if ($window) { break }
        if ($process.HasExited) { throw 'Application exited before opening its window.' }
    }
    if (!$window -or $window.Current.Name -ne 'BrainiacDesk Ultimate') { throw 'Expected application window did not appear.' }
    function Invoke-Button([string] $name) {
        $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name))
        if (!$element) { throw "Missing control: $name" }
        $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 350
    }
    Invoke-Button 'Settings'
    Invoke-Button 'Save preferences'
    Invoke-Button 'Help and tutorials'
    Invoke-Button 'About'
    Invoke-Button 'Dashboard'
    Write-Output 'PASS: Native window launch, Settings save, Help, About, Dashboard navigation.'
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}