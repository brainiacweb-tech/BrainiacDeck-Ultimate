# Dependencies
Direct packages: CommunityToolkit.Mvvm 8.4.0; Microsoft.Extensions.DependencyInjection 10.0.0; Microsoft.Extensions.Logging 10.0.0; Microsoft.Data.Sqlite 10.0.14; SQLitePCLRaw.bundle_e_sqlite3 3.0.5.

These projects use permissive upstream licensing (Microsoft/Toolkit MIT, SQLitePCLRaw Apache-2.0, SQLite public-domain dedication). A full transitive inventory and redistribution notices must be reviewed from restored package metadata before distributing any package. No external executable media engine or OCR binary is bundled. Document/font dependencies are described below.
PDFsharp-WPF 6.2.4 is pinned to its MIT-licensed tag. The upstream notice is preserved in PDFSHARP-LICENSE.txt. Do not upgrade the PDF engine without reviewing the new version's license. Full transitive notice review remains a release gate.

## Interface assets
Raleway Regular, Medium, SemiBold and Bold are embedded as WPF resources from the pinned Google Fonts revision in assets/DESIGN-SOURCES.json. SIL Open Font License 1.1 is retained in src/BrainiacDesk.App/Fonts/OFL.txt and copied into published output. Lucide SVG sources are retained under assets/lucide and converted into native WPF DrawingImage resources. The official ISC license and upstream notices are retained in assets/lucide/LICENSE.txt and published under ThirdPartyNotices. No runtime font/icon download or system font installation is needed.

## Native video
Vortice.MediaFoundation 3.8.3 (MIT; upstream commit 9e609cb9439c9872aa1b339f177e40ec96f77239) binds the Windows Media Foundation H.264 encoder/decoder. Its exact-revision notice is in VORTICE-LICENSE.txt and published under ThirdPartyNotices. No FFmpeg executable or downloaded codec is used. Transitive bindings include Vortice.DirectX 3.8.3, Vortice.Mathematics 2.1.0 and SharpGen.Runtime / SharpGen.Runtime.COM 2.4.2-beta; full redistribution inventory remains a release gate.

## Offline speech and protected content
System.Speech 10.0.0 (Microsoft, MIT) calls the installed local Windows speech recognizer. Windows account protection comes from the Windows Desktop runtime's ProtectedData API; no extra ProtectedData package is necessary. DEPENDENCY-INVENTORY.json records all restored direct/transitive package versions, declared licenses and repository revisions. This inventory is evidence for redistribution review, not a legal approval.

## Brainiac Edge
Tack was studied at revision 973f4446c910f9b354444771205ac4588db41084. No Tack source/assets/identity/updater code was copied. Its complete MIT notice is nevertheless distributed as ThirdPartyNotices/Tack-LICENSE.txt. Edge uses platform Windows.Media.Ocr through the Windows SDK .NET projection; the Windows target is net10.0-windows10.0.19041.0 while the product/package remains Windows 11, WPF and .NET 10. Existing output directories remain stable. No OCR executable or external AI service is bundled. SDK projection redistribution notices must be included in the release dependency review.

## 2026 interface typography and brand assets
The supplied BrainiacDeck logo is retained unchanged at assets/BrainiacDeck-logo.png. The repeatable generate-assets.ps1 pipeline derives PNG/ICO sizes from it for application, tray and package surfaces without changing application identity. Inter is bundled from the official Google Fonts ofl/inter source for offline body/control text; its SIL Open Font License is retained in Fonts/Inter-OFL.txt and copied to published output. Existing Raleway fonts remain the heading family. The hero illustration is original native WPF vector geometry, with no runtime image-generation or remote asset dependency.
