param([string]$Executable = "")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$dotnet = Join-Path $env:LOCALAPPDATA 'BrainiacDesk/dotnet/dotnet.exe'
$dll = Join-Path $PSScriptRoot '../src/BrainiacDesk.App/bin/Release/net10.0-windows/BrainiacDesk.App.dll'
$profile = Join-Path ([IO.Path]::GetTempPath()) ('BrainiacDesk-UI-' + [Guid]::NewGuid().ToString('N'))
$process = $null
function Launch {
        $parameters = @{ FilePath = $dotnet; ArgumentList = @('exec', ('"' + $dll + '"')); WindowStyle = 'Hidden'; Environment = @{ BRAINIACDESK_DATA = $profile }; PassThru = $true }
    if ($Executable) { $parameters.FilePath = (Resolve-Path -LiteralPath $Executable).Path; $parameters.Remove('ArgumentList') }
    $script:process = Start-Process @parameters
    $script:window = Wait-Window 'BrainiacDesk Ultimate'
}
function Wait-Window([string]$title) {
    for ($attempt=0; $attempt -lt 160; $attempt++) {
        $condition=[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$script:process.Id),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$title))
        $element=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        if ($element) { return $element }
        if ($script:process.HasExited) { throw "Application exited while waiting for $title" }
        Start-Sleep -Milliseconds 250
    }
    throw "Window did not appear: $title"
}
function Control([string]$name) {
    for($attempt=0;$attempt -lt 30;$attempt++) {
        $element=$script:window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
        if($element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    $buttons=$script:window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button))
    $names=@($buttons | ForEach-Object {$_.Current.Name}) -join ', '
    throw "Missing control: $name. Available buttons: $names"
}
function Invoke-Button([string]$name) { (Control $name).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 250 }
function Toggle-Off([string]$name) { $pattern=(Control $name).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern); if($pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) {$pattern.Toggle()} }
try {
    Launch
    Invoke-Button 'Get started'
    Invoke-Button 'Settings'
    Toggle-Off 'Use dark appearance (clear for light)'
    Toggle-Off 'Confirm before exiting'
    Invoke-Button 'Save preferences'
    Invoke-Button 'Help and tutorials'
    Invoke-Button 'About'
    Invoke-Button 'Dashboard'
    Write-Output 'PASS: Onboarding, settings save and shell navigation.'
    foreach($card in @(@('Open converter','Smart File Converter'),@('Open shelf','Clipboard Shelf'),@('Open teleprompter','Floating Teleprompter'),@('Open capture','Screen Capture Studio'))) {
        Invoke-Button $card[0]
        $cardWindow=Wait-Window ($card[1]+' · BrainiacDesk')
        $cardWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        Start-Sleep -Milliseconds 300
    }
    Write-Output 'PASS: All four dashboard cards execute their actions.'
    $tools=@{
        'File Converter'='Smart File Converter'; 'Clipboard Shelf'='Clipboard Shelf'; 'Teleprompter'='Floating Teleprompter';
        'File Toolkit'='File Management Toolkit'; 'Workflow Studio'='Workflow Automation Studio'; 'Screen Capture'='Screen Capture Studio';
        'Workspaces'='Workspaces'; 'Shortcuts'='Shortcut Studio'; 'Privacy / Diagnostics'='Privacy, Activity and Diagnostics'; 'Command Center'='Universal Command Center'
    }
    foreach($button in $tools.Keys) {
        Invoke-Button $button
        $tool=Wait-Window ($tools[$button]+' · BrainiacDesk')
        if($button -eq 'Clipboard Shelf') {
            $consent=$tool.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Enable clipboard monitoring for this session (explicit consent)'))
            if(!$consent -or $consent.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Clipboard monitoring was not disabled by default.' }
        }
        if($button -eq 'File Converter') {
            $combo=$tool.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ComboBox))
            if(!$combo) { throw 'Converter output selector is missing.' }
            $expand=$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
            $expand.Expand()
            if($expand.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded) { throw 'Styled format selector did not expand.' }
            $expand.Collapse()
        }
        $tool.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        Write-Output "PASS: $button opens and closes."
    }
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if(!$process.WaitForExit(10000)) { throw 'Graceful app exit timed out.' }
    if($process.ExitCode -ne 0) { throw 'Application exited with error.' }
    Launch
    Invoke-Button 'Settings'
    if((Control 'Use dark appearance (clear for light)').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Theme preference did not survive restart.' }
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if(!$process.WaitForExit(10000)) { throw 'Restarted app did not close gracefully.' }
    Write-Output 'PASS: Full process restart restores preferences; graceful exit succeeds.'
} finally {
    if($process -and !$process.HasExited) { Stop-Process -Id $process.Id }
    # Isolated temporary profile is retained for failure inspection. No real user preferences are modified.
    Write-Output "UI test profile: $profile"
}
