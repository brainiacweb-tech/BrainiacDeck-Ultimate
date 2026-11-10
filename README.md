# BrainiacDesk Ultimate
**One Desktop. Unlimited Possibilities.**

Native offline Windows 11 utilities by FRANCIS KUSI / BrainiacTech Hub. Version 0.2.0 is a development build with working operations; it is not the complete original specification or a Store-ready release.

## Run the built app
Open `artifacts/portable/BrainiacDesk.App.exe`. Keep the entire portable folder together. This is a self-contained x64 build; a separate .NET installation is not required. It is not signed or packaged for the Store.

## Available tools
- Image conversion: PNG, JPEG, BMP, TIFF and first-frame GIF; batch queue, cancellation, resize, JPEG quality, preview, email preset, saved preset, and collision-safe output naming.
- PDF: combine images/PDFs, TXT or Markdown-source to paginated PDF, merge, split, extraction/reordering, reverse pages, rotate pages, and metadata viewing.
- Clipboard Shelf with opt-in encrypted persistence: opt-in session monitoring, manual text/image/file references, search, pin, reorder, smart text copy, expiry, clear, TXT export and teleprompter handoff.
- Floating Teleprompter: independent compact native topmost overlay, smooth scrolling, global play/pause/restart/dismiss controls, protected searchable script library, file import/export, persisted opacity/font/width/speed/geometry, monitor choice, drag/resize/snap and optional genuine offline voice follow. Voice requires session microphone consent and an installed Windows recognizer. Capture exclusion is a request, not a guarantee.
- Local Command Center with deterministic matching, favorites, keyboard navigation, and configurable Ctrl+Shift global hotkey.
- File Toolkit: SHA-256/SHA-512 and comparison, numbered rename with preview/confirmation/undo, byte-verified duplicates, inspection, ZIP creation/preview/safe extraction.
- Manual Workflow Studio: approved input/output folders, extension filter, dry run, ordered copy/hash/ZIP steps, cancellation and per-run logs.
- Workspaces: named tool collections, update/duplicate/delete, safe JSON import/export.
- Capture Studio: desktop/display/region/foreground screenshots, configurable annotations, crop/zoom, HD/social PNG/JPEG/PDF exports and shelf handoff. Native H.264 MP4 screen recording with pause/resume/stop; video trim, crop and zoom editing with separate HD/social exports. Recording and edited MP4 exports are video-only (no audio).
- Local activity, privacy controls, preference export and support diagnostics.

## Build and verify
Use .NET 10 on Windows 11 x64:

    dotnet build BrainiacDesk.slnx -c Release
    dotnet run --project tests/BrainiacDesk.Tests -c Release
    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release
    pwsh -File scripts/ui-smoke.ps1
    dotnet publish src/BrainiacDesk.App -c Release -r win-x64 --self-contained true -o artifacts/portable

The SDK installed for this workspace is `%LOCALAPPDATA%\BrainiacDesk\dotnet\dotnet.exe`. The UI smoke script currently uses that SDK location.

Read docs/READINESS.md for incomplete requirements and verification limits, docs/QA-RESULTS.md for actual results, and docs/HELP.md for offline guidance.
The repository-wide production-readiness audit is in [docs/PRODUCTION-AUDIT.md](docs/PRODUCTION-AUDIT.md). This is a real-world test candidate, with manual Windows and Store packaging gates still open.
