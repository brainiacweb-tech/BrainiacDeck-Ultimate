# Single-window tool navigation

Ordinary tools now open as pages in the existing main window. The sidebar, navbar and search remain available. Converter, Clipboard Shelf, File Toolkit, Workflows, Capture, Workspaces, Privacy, Shortcuts, Command Center and Edge settings reuse their existing working controls and services.

Tool pages retain session state when navigating away. Close tool closes the page and releases its native resources; active operations prevent premature closure. App exit asks about unsaved script edits before releasing services. Clipboard consent remains opt-in; its existing HWND listener is retained without displaying another tool window. Capture continues to hide the visible shell through the existing capture scope. Responsive layouts use the embedded content dimensions.

Teleprompter navigation opens the real script library/editor inside the shell. Use script in overlay explicitly launches the existing floating reading overlay. Brainiac Edge, detached pin cards, recording controllers, image previews, OS file pickers and confirmation dialogs retain their purpose-specific windows.

Implementation: MainWindow.ToolPageHost and ShowToolPage host existing ToolWindow content; ActionRouter shares the shell navigation path; ShellViewModel.NavigationRequested restores ordinary pages even when the selected page name has not changed. The hidden native backing window supports existing clipboard and close lifecycle handling. No database migration or saved-data conversion is required.

Validation includes NavigationTests (--navigation-tests): eight tool routes, same-page Dashboard navigation, session-state retention, clipboard consent, native resource release, command execution, unsaved script retention and running-operation shutdown guards. scripts/ui-smoke.ps1 exercises the published single-window routes and explicit floating overlay separately. See artifacts/release-verification for the latest actual results.
