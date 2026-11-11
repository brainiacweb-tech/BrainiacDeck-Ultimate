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
    $script:window = Wait-Window 'BrainiacDeck Ultimate'
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
    Toggle-Off 'Send minimized windows to the system tray'
    Invoke-Button 'Save preferences'
    Invoke-Button 'Help and tutorials'
    Invoke-Button 'About'
    Invoke-Button 'Dashboard'
    Write-Output 'PASS: Onboarding, settings save and shell navigation.'
    foreach($button in @('Open converter','Open shelf','Open teleprompter','Open capture')) {
        Invoke-Button $button
        Invoke-Button 'Close tool'
        Invoke-Button 'Dashboard'
    }
    Write-Output 'PASS: Dashboard tools open and close inside the shell.'
    foreach($button in @('File Converter','Clipboard Shelf','Teleprompter','File Toolkit','Workflow Studio','Screen Capture','Workspaces','Shortcuts','Privacy / Diagnostics','Command Center')) {
        Invoke-Button $button
        if($button -eq 'Clipboard Shelf') {
            $consent=(Control 'Enable clipboard monitoring for this session (explicit consent)').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            if($consent.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Clipboard monitoring was not disabled by default.' }
        }
        if($button -eq 'Teleprompter') {
            foreach($field in @(@('Script name','UI verification script'),@('Script text','This script was entered through the real packaged application editor.'))) {
                $entry=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$field[0]),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit)))
                $entry.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($field[1])
            }
            Invoke-Button 'Save to script library'
            Invoke-Button 'Use script in overlay'
            $overlay=Wait-Window 'Floating Teleprompter · BrainiacDesk'
            $mainPattern=$window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $mainPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Minimized)
            Start-Sleep -Milliseconds 300
            if($overlay.Current.IsOffscreen) { throw 'Explicit floating overlay disappeared when shell minimized.' }
            $mainPattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
            $close=$overlay.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Close teleprompter'))
            $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        }
        Invoke-Button 'Close tool'
        Invoke-Button 'Dashboard'
        Write-Output "PASS: $button uses the main workspace."
    }
    Invoke-Button 'Brainiac Edge'
    $edgeWindow=Wait-Window 'Brainiac Edge workspace'
    function Invoke-EdgeControl($root,[string]$name) {
        $control=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$name))
        if(!$control) { throw "Missing Edge control: $name" }
        $control.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 250
    }
    Invoke-EdgeControl $edgeWindow 'Add'
    $edgeAdd=Wait-Window 'Add to Brainiac Edge · BrainiacDesk'
    $edgeText=$edgeAdd.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Edit))
    $edgeText.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Edge process restart integration fixture')
    Invoke-EdgeControl $edgeAdd 'Pin text / code / URL'
    $edgeAdd.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Invoke-Button 'Brainiac Edge'
    $edgeWindow=Wait-Window 'Brainiac Edge workspace'
    Invoke-EdgeControl $edgeWindow 'Hide'
    Write-Output 'PASS: Published candidate Edge navigation and real note pinning.'
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if(!$process.WaitForExit(10000)) { throw 'Graceful app exit timed out.' }
    if($process.ExitCode -ne 0) { throw 'Application exited with error.' }
    Launch
    Invoke-Button 'Settings'
    if((Control 'Use dark appearance (clear for light)').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Theme preference did not survive restart.' }
    Invoke-Button 'Brainiac Edge'
    $edgeWindow=Wait-Window 'Brainiac Edge workspace'
    $edgeSearch=$edgeWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Search pinned content'))
    $edgeSearch.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Edge process restart')
    $restoredPin=$null
    for($attempt=0;$attempt -lt 30;$attempt++) {
        $restoredPin=$edgeWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Edge process restart integration fixture'))
        if($restoredPin) { break }
        Start-Sleep -Milliseconds 100
    }
    if(!$restoredPin) { throw 'Edge pin did not survive a full process restart.' }
    Invoke-EdgeControl $edgeWindow 'Hide'
    Write-Output 'PASS: Edge pin survives a full process restart and is searchable through the real UI.'
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if(!$process.WaitForExit(10000)) { throw 'Restarted app did not close gracefully.' }
    Write-Output 'PASS: Full process restart restores preferences; graceful exit succeeds.'
} finally {
    if($process -and !$process.HasExited) { Stop-Process -Id $process.Id }
    # Isolated temporary profile is retained for failure inspection. No real user preferences are modified.
    Write-Output "UI test profile: $profile"
}
