# BrainiacDesk Ultimate
One Desktop. Unlimited Possibilities.

Native Windows 11 productivity application by FRANCIS KUSI / BrainiacTech Hub.

This is a Milestone 1 foundation development build, not the full requested utility suite.
Implemented: onboarding, dashboard, MVVM navigation, light/dark themes, SQLite preferences, tray restore/exit, exit confirmation, offline help and about.

## Build and run
Install the .NET 10 SDK on Windows 11 x64. Open BrainiacDesk.slnx in a compatible Visual Studio version, or run:

    dotnet build BrainiacDesk.slnx -c Release
    dotnet run --project tests/BrainiacDesk.Tests -c Release
    dotnet run --project src/BrainiacDesk.App -c Release

The session-local SDK is at `%LOCALAPPDATA%\BrainiacDesk\dotnet\dotnet.exe`.

Settings are stored in `%LOCALAPPDATA%\BrainiacDesk\settings.db`. No account, analytics, clipboard monitoring, or cloud service is implemented.
See docs/READINESS.md for limitations and verification status.