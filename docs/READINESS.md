# Readiness — 0.2.0 development build
This is a substantial working implementation, not completion of every item in the original specification. No milestone is declared fully accepted; development has added tested operation slices across the modules, with the gaps below still open.

## Implemented and operation-tested
SQLite preference persistence, known-vector SHA-256/SHA-512, duplicate size/hash/byte verification, numbered rename/undo/collision safeguards, ZIP round trip and malicious-path rejection, ZIP cancellation, workflow folder permissions and loop prevention, copy/hash/ZIP workflow execution, five image output formats/resizing, failed/corrupt/cancelled conversion, output preservation, image/text PDF creation, PDF extraction/reordering/page ranges/merge/rotation, text transformations, screenshot crop/redaction/annotation rendering, deterministic command matching.

## Implemented and UI smoke-tested
Onboarding completion; Settings, Help, About and Dashboard navigation; all ten tool windows open/close; clipboard monitoring checkbox defaults off; saved theme restored across a full process restart; clean app exit with confirmation disabled.

## Implemented but requiring additional Windows verification
Live clipboard capture/copy and monitoring unregister; tray interactions and confirmation dialogs; full live desktop recording and foreground/region selection acceptance; capture exclusion with specific recorders; teleprompter timing, keyboard focus and overlay behavior; global shortcut conflicts; drag/drop; workspaces and presets through the full UI; cancellation during large real batches; PDF split UI; high DPI and multi-monitor behavior. UI smoke tests exercise window creation, not every operation.

## Remaining specification gaps
- WebP, rich Markdown rendering, DOCX, PDF image extraction/compression, password-protected archives are not included. Native silent video capture/editing is implemented; no external media engine is bundled. GIF/TIFF conversion uses only the first frame.
- Converter lacks circular format UI, multi-preset management. It has protected retained conversion history, one saved preset and file-level status, not codec-internal progress.
- Clipboard shelf has one collection with optional protected persistence, fixed 50-item/one-hour retention, basic pinning and search. Named shelves/categories, process exclusions, adjustable retention/history limits, startup integration is not implemented. Privacy filters cannot reliably identify secrets.
- Teleprompter now has an independent native overlay, protected searchable script library, persisted geometry/settings and genuine optional offline speech recognition. Real microphone/meeting-app compatibility, monitor hot-plug and mixed-DPI interactions require manual acceptance. Recorder exclusion is not guaranteed.
- Command actions open tools; contextual file inputs and command file-drop pipelines are not implemented. Shortcut Studio manages one command-center global hotkey.
- Workflow triggers are manual only; copy/hash/ZIP steps use the original selected source set. Folder monitoring, schedules, clipboard triggers, conversion/rename/move steps, sophisticated conditions and visual connection editing remain deferred.
- File toolkit lacks advanced rename patterns, automatic duplicate cleanup and temporary-file cleanup. It does not delete duplicate files.
- Workspaces store named tool collections and a color value; layout restoration, custom icons and workspace shortcuts are deferred.
- Capture now integrates Windows offline OCR and the persistent Brainiac Edge pinboard; see BRAINIAC-EDGE-REPORT.md for tested scope. Redaction is opaque masking rather than blur. Desktop region selection, configurable annotations, HD/social exports, native silent MP4 recording, and trim/crop/zoom export are implemented. Captures over 32 megapixels are rejected; shelf images over 8 megapixels are rejected. Recording has no microphone/system audio, and video editing omits imported audio. Native codec support depends on Windows; editor supports frames up to 3840 pixels per side and rejects changing presentation dimensions. Long-session performance, mixed-DPI region selection and live recording/toolbar exclusion require manual acceptance.
- System theme, UI density, general animation preferences, optional startup registration, full navigation customization and app-wide reset are not implemented.
- Shell is MVVM; utility windows currently contain UI coordination code behind. Shared file/workflow contracts are in Core, and codec/platform operations are isolated helpers; full MVVM separation for all modules remains work.
- No profiling numbers, completed accessibility/security/license audit, multi-monitor/DPI certification, or Store approval. Full dependency notices and trademark/name verification remain required.

## Packaging
Original PNG/ICO assets and a .wapproj/manifest/package script exist. Self-contained x64 publishing is supported. Windows SDK MakeAppx/SignTool were not found in the checked environment. Publisher identity is deliberately a placeholder. No MSIX, signing, installation, upgrade, uninstall, WACK or Store certification result is claimed.
See PRODUCTION-AUDIT.md for the repository-wide audit, actual data sources and remaining release gates.

Brainiac Edge acceptance and verification are documented in [BRAINIAC-EDGE-REPORT.md](BRAINIAC-EDGE-REPORT.md). Store signing, mixed-DPI/hot-plug and packaged uninstall remain release gates.
