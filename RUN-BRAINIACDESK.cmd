@echo off
if exist "%~dp0artifacts\theme-fix\BrainiacDesk.App.exe" (
    start "" "%~dp0artifacts\theme-fix\BrainiacDesk.App.exe"
) else (
    start "" "%~dp0artifacts\portable\BrainiacDesk.App.exe"
)
