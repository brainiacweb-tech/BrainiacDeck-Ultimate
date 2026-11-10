# Production-readiness audit — 2026-11-10
This is an engineering audit of the source repository and a real-world test candidate. It is not Microsoft Store approval or a completed manual Windows acceptance report.

## Repository scope and mock-data findings
Reviewed all App, Core, Infrastructure and Package sources, build/packaging/CI scripts, tests, assets and release documentation. Runtime searches for mock/dummy/demo/simulated/TODO/not-implemented/random-data paths found no seeded user records, fake conversion service, generated usage statistics or demonstration workflow repository. Media Foundation variables named `sample` are real decoded/encoded media samples. Capture delays are genuine countdown/overlay-settle delays. The startup indeterminate indicator accompanies actual local-settings initialization and does not claim a numeric completion percentage. Synthetic fixtures exist only in tests and ignored review artifacts; they are not shipped as user content.

First use has no saved scripts, shelf content, operation history, workflows, or workspaces. Built-in action definitions and default preferences are functional configuration, not fabricated user activity. Dashboard counts read actual saved scripts/workspaces and successful conversions in the retained operation journal. They are not lifetime totals.

## Module findings and changes
| Module | Genuine implementation and changes | Practical limits |
|---|---|---|
| Converter | Windows image codecs and PDFsharp; actual image/PDF queue outputs; real per-file errors/byte sizes; protected durable history; incompatible email preset disabled | First GIF/TIFF frame; Markdown source text; no WebP/DOCX/compression promise |
| Clipboard | Native clipboard listener and explicit session monitoring consent; actual text/image/file references; opt-in Windows-account-encrypted persistence; saved privacy filters; repaired collapse | 50 items, unpinned one-hour expiry, 8 MP images; saved archive 24 MB; secret filters cannot guarantee detection; file items do not copy files |
| Teleprompter | Independent native topmost/no-activate window; real text viewport scrolling; play/pause/restart; global hotkeys; protected searchable script library; real file import/export; persisted geometry/settings; monitor selection; capture-exclusion request | Speech requires an installed compatible offline recognizer and explicit microphone consent; no claim of meeting-audio isolation or perfect recognition |
| Commands/shortcuts | Deterministic search dispatches actual tools; favorites and registered shortcut preferences persist; Win32 registration reports conflicts | Commands open tools; no advertised shell execution/file pipeline |
| Automation | Validated manual Copy/SHA-256/ZIP operations on explicitly selected source files; reviewed execution and actual logs; saved definition | Each step uses source set; no watcher/scheduler/pause implementation; renamed control to Cancel |
| Capture | Actual desktop pixels, region selection, image editing/export, native silent H.264 recording/editing; last recording restored from successful history | No audio/OCR; codec/monitor/protected-content compatibility varies |
| File Toolkit | Actual hashes/byte-verified duplicates, metadata, reviewed rename/undo, safe ZIP operations | No automatic duplicate deletion; rename undo is session-scoped |
| Workspaces | Real saved named tool collections, import/export, launch, duplication/deletion; color visible; empty selection errors | No promise to restore third-party apps or desktop layouts |
| Settings/privacy | SQLite preferences; real activity/operation clearing, saved-data deletion, preference/diagnostic exports; exit guard covers independent editors | DPAPI is bound to the current Windows account; exported TXT/files are unencrypted; no startup registration |

Operation history retains up to 200 actual results with file paths and outcomes. Activity retains up to 200 real module-opening/workflow-completion events. Ordinary preferences/workflows/workspace definitions are not encrypted. Content in the script library, optional shelf archive and operation journal uses Windows account data protection. Storage corruption is reported without overwriting existing data with an empty replacement. Histories and clipboard data are not imported as demonstration records.

## Native teleprompter behavior
The overlay has no main-window owner, uses WPF Topmost plus a native non-activating extended style, and handles mouse activation without taking foreground focus. Ordinary playback controls stay non-activating; opening script/settings editors intentionally permits keyboard focus. It uses a compact original navy rounded panel, white text, native vector controls, hover extras and a resize grip. Paused scrolling stops its frame timer. Geometry is saved before destroying the native handle; disconnected/off-screen saved monitors fall back to a current screen and clamped working bounds. Global Ctrl+Alt+P/R/X registrations are session-scoped, report conflicts and unregister on close.

Voice follow uses System.Speech with an installed local recognizer and dictation grammar. It requests application microphone consent, opens the default input only after consent, matches consecutive recognized words near the current script position, and releases the recognizer on stop/close. Unmatched speech does not invent progress. Windows desktop microphone access is controlled in Windows Settings; the app links to that page rather than pretending a browser-style permission prompt exists.

Capture exclusion uses supported SetWindowDisplayAffinity and explicitly remains a request: test Teams, Zoom, Meet and each recorder before relying on it. No invisibility guarantee is made.

## Required manual acceptance and release gates
- Microphone permission-denied/device-disconnected paths and voice alignment with real speakers, accents, noise and meeting applications; no microphone was automatically activated by the tests.
- Real keyboard shortcut delivery/conflicts, mouse drag/resize/hover, monitor hot-plug, mixed DPI, taskbar work areas, Narrator/contrast and keyboard-only editing.
- Teams/Zoom/Meet foreground focus and capture-exclusion behavior, full-screen apps, tray lifecycle, extended recording sessions and device load.
- Final dependency redistribution/legal/trademark review, published privacy/support URLs and real publisher identity.
- Windows SDK MakeAppx/SignTool/WACK were not found in the checked environment. Supply Partner Center identity, build/sign a real package on a configured machine, then verify clean install, offline operation, upgrade retention, uninstall and WACK. The packaging script refuses an unassigned publisher. No identity, signing certificate, certification result or Store readiness is fabricated.

See QA-RESULTS.md for command results and READINESS.md for deliberately unadvertised feature gaps. The application is a test candidate until the manual gates above are completed.

## Platform references
- Microsoft offline recognition API: https://learn.microsoft.com/en-us/dotnet/api/system.speech.recognition.speechrecognitionengine
- Windows microphone privacy: https://learn.microsoft.com/en-us/windows/apps/develop/camera/camera-privacy-setting
- Window capture affinity: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- Extended window styles: https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles