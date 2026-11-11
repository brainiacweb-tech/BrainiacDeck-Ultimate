# Brainiac Edge implementation report

Verified 2026-11-11. Brainiac Edge is integrated into the existing BrainiacDesk Ultimate executable. It retains C#/.NET 10, WPF, CommunityToolkit.Mvvm, SQLite, existing theme resources, module routing and Store packaging identity. No existing module was replaced.

## Working features

* Floating transparent WPF shelf, normally hidden, with native topmost placement. Event-driven activation strips support four display edges, configurable dwell/sensitivity and monitor selection. Reveal/tuck animations respect reduced motion; fullscreen/presentation detection suppresses automatic reveal. Pointer reveal does not activate the keyboard; deliberate clicks and the global shortcut do. Width, height, alignment, monitor, docking and presentation settings persist.
* Dashboard/navigation, Settings, Shortcut Studio and Command Center entry points. Default shortcut is Ctrl+Shift+E; conflicts retain the previous registration. Command Center supports opening/hiding Edge, pinning the selected Shelf item, pinning the latest session screenshot, searching all collections, expiry cleanup and Edge settings. Existing command order and matching remain intact.
* Persistent text, notes, code, HTTP/HTTPS URLs, images, screenshots, PDF/Word/Excel documents, folders and other local-file references. Image cards use genuine asynchronously decoded thumbnails. Images open at original resolution; documents/files open with their installed Windows handler. Pinning/removal never moves or deletes original files.
* Capture Studio handoffs: explicit pinning, latest-session screenshot pinning, independently opt-in automatic pinning, offline OCR and recognized-text handoff to Clipboard Shelf. Capture editor annotations/crop/redaction/export are reused. Pixel-based fingerprints deduplicate repeated screenshots and matching image-file drops within a collection.
* Existing Clipboard Shelf selected-item handoff, current text exclusion filtering and explicit pin-to-Edge action. Edge adds no clipboard listener, database or permanent monitoring service. Shelf session monitoring remains off until its existing consent checkbox is enabled.
* Local Windows OCR and SQLite FTS5 search over title, text, tags and recognized text. Queries match word prefixes; favourites, type, recent and workspace filters are available. Missing Windows OCR languages produce an actionable error. No private content is sent to cloud OCR or AI services.
* Detachable native cards with dragging, resizing, opacity, optional always-on-top, copying, editing through the pin editor, pin protection, minimize, close and restore. Closing retains the underlying pin; finite restore geometry persists.
* Metadata editing, favourites, pin/unpin, tags, multi-selection, bulk pin/favourite toggles, bulk removal, search, missing-file relinking and expiry. Unpinned expiry is applied on startup/reveal and through manual cleanup; original files remain untouched.
* Workspace collections use stable IDs added to the existing workspace records. Legacy records are migrated without replacing names/actions; update retains IDs, duplicate/import creates new IDs. Opening a workspace selects its Edge collection. All collections exposes orphaned collections after workspace deletion. User-created Coding/Study/Research/Business/Presentation workspaces require no sample content.
* Workflow Studio can pin the reviewed source references to the selected Edge collection after successful copy/hash/ZIP execution. Its explicit image workflow composes pinboard, Windows OCR, existing Shelf filtering and existing Converter PDF output. Partial results remain visible if a later operation fails.
* Local merge backup/restore includes managed screenshots, preserves existing pins, rejects unsupported backup/schema versions, writes metadata transactionally and does not execute restored links/files. Managed screenshots restore under the destination profile. External files remain references. Optional manual cleanup is restricted to unreferenced managed PNGs older than one day.

## Integration map

| Area | Actual files and services |
| --- | --- |
| Contracts and rules | Core/Edge.cs: EdgeItem, EdgeSettings, IEdgeStore, dwell/edge geometry rules |
| Persistence | Infrastructure/EdgeStore.cs: additive settings.db tables, schema versions, FTS5 triggers, deduplication, retention and backup |
| MVVM state | App/EdgeViewModel.cs: observable collection, search/filter state, loading/error/empty states, asynchronous refresh |
| App lifetime | App/EdgeCoordinator.cs, MainWindow.xaml.cs, ActionRouter.cs: one coordinator, independent hotkey ID, event/native disposal |
| Native activation | App/EdgeNative.cs: transparent activation windows, Win32 placement/DPI and fullscreen detection, display-change subscription |
| Shelf/cards | App/EdgeWindow.xaml(.cs), EdgeCardWindow.cs, EdgeDialogs.cs |
| Content handling | App/EdgeContentService.cs: explicit pin/import/copy, pixel fingerprints, frozen image sources; no clipboard listener |
| Screenshot/editor | App/CaptureWindow.cs, CaptureVisibilityScope.cs: completion handoff, direct editor load, capture-time activation suspension |
| Clipboard | App/ClipboardShelfWindow.cs and unchanged ShelfPersistence.cs listener/protection model |
| OCR | App/LocalOcr.cs: Windows.Media.Ocr and installed local language packs |
| PDF/conversion | Existing App/ConversionEngine.cs; no replacement PDF/conversion engine |
| Workspaces/workflows | App/WorkspaceWindow.cs and WorkflowWindow.cs, existing Core/WorkflowEngine.cs authorization retained |
| Design | Existing theme values/styles moved unchanged from App.xaml to Design/Theme.xaml so both production and test hosts use the same resources |
| Navigation/settings/history | MainWindow.xaml, ShortcutWindow.cs, PrivacyWindow.cs, existing ActivityLog/OperationJournal |
| Verification | EdgeTests.cs, EdgeWindowsTests.cs, existing executable harnesses, ui-smoke.ps1, verify-release.ps1 and Windows CI |

Paths above are relative to src/BrainiacDesk.* unless identified as tests/scripts. The Windows framework target now includes the Windows SDK version (net10.0-windows10.0.19041.0) for platform OCR projections; the application remains WPF/.NET 10 and its manifest still targets Windows 11. Existing output directories remain stable.

## Storage, privacy and resource ownership

Edge tables share settings.db with existing preferences; Shelf protected storage is unchanged. Managed images live in %LOCALAPPDATA%/BrainiacDesk/Edge/Images, or the existing BRAINIACDESK_DATA test/profile override. Edge pins and JSON backups are ordinary unencrypted local content; this is stated in the Add flow, Privacy Center, help and privacy policy. Explicitly pinning Shelf content persists that selected content in Edge even when Shelf persistence is off.

The shelf virtualizes/recycles list rows. Thumbnail decoding uses two concurrent workers, frozen sources and a bounded 64-image cache; recycled rows clear old images. Search runs off the UI thread and rejects stale refresh results. Hidden activation uses input events and a dwell timer, not a permanent mouse polling loop. Visible auto-hide checks run every 750 ms. Sensor windows, display subscriptions, hotkey hooks, card windows and shelf timers are disposed at app shutdown. Capture scopes suspend automatic activation. Native secure-desktop/protected-content boundaries are not bypassed.

## Reference analysis and licensing

Tack was cloned outside this repository to ../BrainiacDesk-References/tack at 973f4446c910f9b354444771205ac4588db41084. The feature-by-feature static analysis and implementation plan are in [BRAINIAC-EDGE-PLAN.md](BRAINIAC-EDGE-PLAN.md). Tack was not executed, so its runtime behavior is not claimed as observed.

No Tack source, logo, signing keys, updater settings or identifiers were copied. Its complete MIT copyright and permission notice is retained and distributed in ThirdPartyNotices/Tack-LICENSE.txt. Windows SDK projection metadata/terms are separately recorded in the dependency inventory and Windows-SDK-NET-Ref.txt. [Microsoft's OCR API documentation](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.recognizeasync) documents the platform recognition API used here.

## Verification results

The full release script completed successfully with an isolated profile and candidate publish:

    ./scripts/verify-release.ps1 -Interactive -Output artifacts/edge-candidate

| Check | Result |
| --- | --- |
| Release solution build | Passed, zero warnings and errors |
| Core/SQLite harness | 36 passed |
| Existing Windows conversion/PDF/capture/video harness | 56 passed |
| Edge native Windows integration harness | 27 passed |
| Existing Teleprompter/protected persistence/module UI harness | 35 passed |
| NuGet direct/transitive vulnerability audit | Passed; no reported vulnerable packages in the audited dependency graph |
| Self-contained win-x64 publish | Passed |
| Published executable UI smoke | Passed: onboarding, settings, all existing tools, clipboard consent, graceful exit and restart |
| Expanded Edge full-process restart smoke | Passed: pin a note through the actual Add UI, exit, relaunch and find the persisted pin through Edge search |
| git diff --check | Passed |

That is 154 executable harness checks, plus the application smoke flows. Logs and release results are under artifacts/release-verification; the additional restart smoke log is edge-process-restart-smoke.log. The visual artifact is artifacts/ui-review/brainiac-edge.png. Native Edge tests exercise actual WPF windows, Win32 hotkey registration/conflict rollback, WM_HOTKEY dispatch, physical placement on the available display, native resource shutdown, real Windows OCR, bitmap deduplication, Shelf handoff without monitoring consent, and managed-image backup restoration. Activation and file-drop events in the automated harness are routed test events; these do not prove every physical mouse/OLE path.

Failures found and fixed during development included command catalog ordering, a test-host startup conflict, a floating-card close/geometry crash, a UTF-8 source-write interruption, and an auto-hide assumption in the expanded UI test. Results above are the successful reruns, not claims that those initial runs passed.

## Remaining limitations and acceptance gates

* Physical multi-monitor testing was limited to the one attached display. Negative coordinates and dwell rules have automated coverage; mixed-DPI transitions, monitor hot-plug, every docking/alignment combination and performance during long desktop sessions still need manual hardware acceptance.
* Fullscreen suppression is implemented through Windows notification state and foreground bounds. Real games, presentation tools, system dialogs, shell edge gestures, secure desktop and external applications' shortcut conflicts still need manual acceptance. The automated shortcut conflict uses two actual registrations in the test process.
* PDF and Office pins provide type/path references and native opening; they do not embed an Office/PDF document renderer. Code editing is plain text. Glass style is translucency, without a new DWM blur dependency. Compact/expanded presentation is controlled by thumbnail/shelf dimensions.
* Closing a detached card retains its pin and saved geometry; cards do not reopen automatically at startup. Recent is the newest 50 pins. Expiry is checked on startup/reveal or manually, not by a continuously running retention job.
* The pin backup format is capped at 32 MB and includes managed screenshots only. It does not bundle external documents, workspace definitions, Edge settings or card-placement preferences; back up the full profile for those. Existing IDs remain visible through All collections if workspace definitions are absent. No screenshot-folder watcher was added: automatic capture integration receives BrainiacDesk Capture Studio completions.
* Clipboard Shelf still limits images to 8 megapixels and text to its existing limits/filters; Edge/editor images support 32 megapixels. Native image decoding, OCR accuracy and installed language coverage remain platform-dependent.
* Workflow triggers remain explicit/manual apart from separately enabled capture handoffs. The existing file workflow engine is not replaced with a new general automation scheduler.

## Microsoft Store readiness

The existing packaging approach, application identity and runFullTrust capability are retained. app.manifest still requests asInvoker; Edge introduces no administrator requirement, external updater, signing identity or protected-content bypass. The self-contained desktop candidate was tested, not an installed signed MSIX.

Store release is **not ready**: the manifest still contains the Partner Center publisher placeholder; makeappx/signtool/appcert were not available as commands in this environment. A real publisher identity, signed package installation/update tests, Windows App Certification Kit, packaged WinRT/native API checks, SDK projection redistribution review, and uninstall/profile-cleanup validation remain required. No clean-uninstall or Store certification success is claimed.

Run artifacts/edge-candidate/BrainiacDesk.App.exe with the entire candidate folder intact. This is another build of the existing BrainiacDesk Ultimate application, not a separate product or framework migration.
