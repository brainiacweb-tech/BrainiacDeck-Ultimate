# Privacy policy — development build
BrainiacDesk Ultimate by FRANCIS KUSI / BrainiacTech Hub processes files locally. It has no analytics, advertisements, required account, remote database, cloud OCR or cloud AI. Core tools work offline after installation.

Local data under %LOCALAPPDATA%\BrainiacDesk includes a SQLite preference database, an optional conversion preset (including its output folder), workflow definitions with folder paths, workspace tool collections, shortcut preferences, command favorites, and at most 200 module-opening/completion activity events. These ordinary configuration files are not encrypted; do not put secrets in names or settings. An explicit BRAINIACDESK_DATA environment override can select another local profile for testing.

Clipboard monitoring starts off and requires a session-only consent checkbox. Manual clipboard reads require a button click. Shelf content stays in process memory, with a 50-item limit and one-hour expiry for unpinned items. Closing the shelf clears content and removes its monitoring registration. Clipboard text filters are limited and cannot guarantee password detection: pause before copying private content. Other apps and Windows may maintain their own clipboard history.

Screenshots and annotations stay in memory until copied or exported. Undo may retain unredacted pixels in memory, while exports flatten opaque redaction. PDF screenshot export uses a temporary local image and deletes it afterward; this is not secure disk erasure. Scripts are saved only to user-chosen plain-text files. Exported/conversion/workflow files remain in the chosen directories. No automatic user-file deletion or background workflow monitoring is implemented.

Privacy controls clear activity, remove saved configurations, export preferences, and generate OS/runtime-only support diagnostics. They do not delete exported files, scripts or screenshots from external folders. There is no startup integration. Exit stops the process and its listeners.

Before public distribution, the developer must publish this policy at a stable URL and provide a real support/privacy contact. No contact details are invented here.