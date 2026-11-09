# Build and package
Use Windows 11 x64 with .NET 10. The workspace-local SDK is %LOCALAPPDATA%\BrainiacDesk\dotnet\dotnet.exe.

    dotnet build BrainiacDesk.slnx -c Release
    dotnet run --project src/BrainiacDesk.App -c Release
    dotnet publish src/BrainiacDesk.App -c Release -r win-x64 --self-contained true -o artifacts/portable

Open BrainiacDesk.slnx in a Visual Studio version supporting .NET 10. The standalone packaging project at src/BrainiacDesk.Package requires Visual Studio's Windows Application Packaging tools and Windows SDK 10.0.26100.0; it is kept out of CLI solution builds so missing packaging tools do not block source verification.

Regenerate original icon assets with scripts/generate-assets.ps1. Set the real Partner Center identity in Package.appxmanifest before running scripts/package-msix.ps1. The package script fails intentionally while a placeholder publisher remains. Pass -Dotnet and -MakeAppx explicit paths if those tools are not on PATH. Never commit private signing keys.