# Brainiac Edge integration plan

## Inspected baseline
The source inventory covers Core, Infrastructure, App, package manifest, build scripts, both executable test harnesses and readiness documentation. Existing utility coordinators use code behind; ShellViewModel uses CommunityToolkit.Mvvm. New Edge state will use a view model without restructuring existing tools.

## Reference analysis
Tack cloned separately to ../BrainiacDesk-References/tack, revision 973f4446c910f9b354444771205ac4588db41084. Static source analysis, not an observation of its running UI.

* Tack.Windows/Edge/EdgeWatch.cs, EdgePoller.cs and EdgeStrips.cs: transparent native edge strips wake a dedicated pointer thread; dwell and jitter rules distinguish edge hover and OLE drag. A blocking queue parks polling when idle; fallback polling exists if strips fail. Dispose stops threads and strips.
* Tack.App/Reveal.cs: synchronized view generations reject stale asynchronous show/hide operations; pointer reveals avoid keyboard activation, explicit shortcuts hold open and obtain focus. Placement follows monitor work area and scale. FullScreenCheck.cs suppresses automatic reveal during fullscreen/presentation, with a brief grace for Snipping Tool overlays.
* Tack.Windows/Display/Monitors.cs: MonitorFromPoint, GetMonitorInfo, GetDpiForMonitor, EnumDisplayMonitors and SHQueryUserNotificationState provide physical bounds, effective DPI and fullscreen state.
* Tack.App/Pinboard/BoardWindow and Reveal: WinUI board presentation and reveal/tuck feed, native hide after the animation. We will implement WPF transforms and reduced-motion handling independently.
* Tack.Windows/Screenshots/FolderWatch.cs and Tack.App/ScreenshotFiles.cs: FileSystemWatcher, bounded native buffer, write-settle retries and deletion grace. New files pin; edits redraw; disappearance can unpin. Edge instead receives existing BrainiacDesk capture completion directly; it must retain missing references.
* Tack.Windows/Clipboard: dedicated clipboard thread, data-format adapters and privacy markers (ExcludeClipboardContentFromMonitorProcessing, Clipboard Viewer Ignore, history allowance). Edge will not copy this monitoring design; ClipboardShelfWindow remains the only listener.
* Tack.Windows/Hotkeys/GlobalHotkeys.cs: RegisterHotKey on host UI thread; independent chord conflict status and disposal. Extend BrainiacDesk ShortcutService with distinct registration IDs and conflict rollback.
* Tack.App/DragOutFlow.cs: mark active drag to inhibit tuck; wait for pending saves; recheck path after drag. Edge exports only Copy effects and never moves source files.
* Tack.Core/Storage/BoardFile.cs: versioned JSON references and atomic replacement, damaged/newer input backup before overwrite. Edge will use additive SQLite tables in settings.db, bounded imports and transactions.
* Tack.Core/Files/LinkedFile.cs and storage: linked versus owned files; cleanup must distinguish ownership. Edge will never delete original files and will constrain managed image cleanup to its own directory.
* Thumbnail processing is separated from persisted references; native imaging/shell resources and clipboard/watch threads have explicit lifetime ownership. Edge uses frozen, decode-limited WPF thumbnails, a bounded cache and disposable activation/native subscriptions.

MIT copyright: Copyright (c) 2026 Prajjwal Yadav and the Tack contributors. Preserve full license in distributed ThirdPartyNotices. No Tack assets, signing material, identifiers or updater settings are reused.

## Incremental implementation
1. Add Core Edge contracts, validated settings, identity/dwell rules; Infrastructure EdgeStore with additive schema, parameterized queries, duplicate constraints, search, transaction-based backup restore and expiry. Run build and Core harness.
2. Add EdgeViewModel, Edge content service, native WPF shelf and detachable cards, independent shortcut registration, desktop/DPI/fullscreen handling, settings UI, keyboard operations, thumbnails, error/empty states. Run build and Windows harness.
3. ActionRouter/MainWindow: lifecycle, navigation, command catalog, activity. ClipboardShelfWindow: explicit selected-item handoff with existing privacy filtering. CaptureWindow: opt-in completion handoff and existing editor load API. ConversionEngine stays the PDF implementation. Add Windows offline OCR (none currently exists).
4. WorkspaceWindow: backwards-compatible stable IDs (none currently exist), preserve on update, generate on duplicate/import, select Edge collection when opened. WorkflowWindow: optional pin completed reviewed source files using a callback; retain original engine authorization and approval boundaries.
5. Add persistence/duplicate/migration/expiry/restore tests and Windows geometry/content/shortcut lifecycle checks; run both existing harnesses and UI smoke. Document actual results and manual gates.

## Store and acceptance boundaries
Retain WPF .NET 10, package identity/manifest and no-admin desktop APIs. Existing Store publisher placeholder and signing gates remain. Interactive mixed-DPI, real fullscreen apps, secure desktop, shortcut conflicts with external apps and packaged uninstall require manual acceptance; do not label these passed from static code. No protected-content bypass or external AI/network content processing.
