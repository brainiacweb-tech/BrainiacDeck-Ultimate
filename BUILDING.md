# Building
Use Windows 11 x64 and the .NET 10 SDK. Run `dotnet build BrainiacDesk.slnx -c Release`. Launch with `dotnet run --project src/BrainiacDesk.App -c Release`. No elevation is required by the application. Restore requires NuGet network access; the built application operates locally.

The SDK installed during development is at `%LOCALAPPDATA%\BrainiacDesk\dotnet\dotnet.exe`; use its full path when dotnet on PATH points to .NET 8. Visual Studio must support .NET 10 and .slnx solution files.