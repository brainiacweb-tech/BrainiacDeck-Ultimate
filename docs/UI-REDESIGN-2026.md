# BrainiacDeck interface audit and implementation plan

## Baseline inventory
Existing C#/.NET 10 Windows WPF solution: Core rules/file operations/workflows, Infrastructure serialized SQLite settings/Edge storage, App WPF composition with CommunityToolkit.Mvvm shell state and utility-window coordinators. No account/authentication subsystem, presentation-slide editor, or separate project document model exists. Existing data must remain compatible.

Routes: Dashboard, onboarding, Settings, Help, About; ActionRouter tools convert, shelf, prompt, files, workflow, capture, workspaces, privacy, edge and Edge action commands. Dialogs include script library/settings, shortcut settings, image preview, capture region selection, recording controls, video editing, Edge add/edit/settings and detachable cards.

Implemented operations: image/PDF conversion and exports; protected opt-in Clipboard Shelf and session listener; Teleprompter real scripts, protected library, smooth scrolling, mirror, geometry, hotkeys and optional offline speech; file hash/duplicates/rename/ZIP; reviewed manual file workflows and Edge image pipelines; capture annotations/region/video editing; stable workspace IDs and import/export; SQLite Edge collections/OCR/FTS, native sensors/hotkeys/cards; protected operation journal and ordinary bounded module activity.

Styling: Design/Theme.xaml plus native vector Design/Icons.xaml, bundled Raleway. ShellViewModel currently replaces six brush tokens; ToolWindow is the common header/body/status wrapper. Converter/Capture use grids with fixed side columns; Teleprompter deliberately uses independent nonactivating native overlay and custom reading colors. Those canvas/export colors must not be indiscriminately recolored.

## Implementation sequence
1. Shared light/dark tokens, Inter body font, Raleway heading primitives, control/focus/selection templates, existing icon set and supplied logo resources. Preserve saved themes and all data paths/identities.
2. Three-region responsive shell: rounded sidebar with collapsible tool categories, actual global search, hero with workspace creation/tool exploration, actual continuation/activity/saved outputs and conditional right widgets. Projects means actual existing workspace collections; no fictional slide/project model or user profile.
3. Shared tool-window chrome/cards, consistent module styles, responsive Converter/Capture layouts, refreshed Edge surfaces and recording toolbar. Teleprompter gets real alignment/colors/reading guide/vertical flip/fullscreen controls while retaining native no-focus behavior, scrolling, speech, library and hotkeys.
4. Build and run Core, Windows, Teleprompter and Edge harnesses; add meaningful theme/search/state tests; render real light/dark/resized screens and run full-process UI smoke and candidate publish.

## Risks and acceptance
Maintain control automation names, action IDs, persistent payloads and original file safety. Add only optional backwards-compatible preference fields. Unavailable saved files must show missing status. Dashboard counts come from retained data, not invented productivity. Theme dark surfaces need their own pastel equivalents. System file dialogs remain OS-native. Mixed-DPI/physical monitor acceptance and Store publisher/signing gates remain separate from source/UI checks. Preserve earlier uncommitted Brainiac Edge work.

## Verified outcome
Implementation and actual verification results are recorded in [UI-REDESIGN-REPORT.md](UI-REDESIGN-REPORT.md). The complete release verification and full-process candidate smoke passed.
