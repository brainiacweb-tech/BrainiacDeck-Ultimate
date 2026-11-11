# Verification
Run both executable integration harnesses (not VSTest projects):

    dotnet run --project tests/BrainiacDesk.Tests -c Release
    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release

Then run `pwsh -File scripts/ui-smoke.ps1` on an interactive Windows desktop. It uses a unique temporary profile, verifies first-run setup, navigates the shell, opens/closes all tools, checks clipboard consent defaults, and verifies preferences across graceful exit/restart. Temporary profiles are retained for inspection. Normal profile preferences and clipboard contents are not modified by this smoke test.

`dotnet test` does not execute these harnesses. UI tests do not prove every control, hotkey, tray interaction or real capture path. See docs/QA-RESULTS.md and docs/READINESS.md for exact scope and pending manual tests.
Run native overlay/persistence and real converter/shelf/workspace/command/workflow UI-operation checks on an interactive Windows desktop:

    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release -- --teleprompter-tests

This uses isolated local fixtures and encrypted profiles. It does not enable the microphone, send meeting input, or modify the normal clipboard. See docs/PRODUCTION-AUDIT.md for required live speech/meeting/DPI acceptance.

Run the Brainiac Edge native integration harness on an interactive Windows desktop:

    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release -- --edge-tests

It uses an isolated profile, real WPF windows, a synthetic routed activation/drop event, actual Windows hotkey registrations and local Windows OCR. It does not prove physical mixed-DPI hot-plug, external-app shortcut conflicts, fullscreen gaming or Store uninstall. The release verification script includes Edge checks with -Interactive.

Run the actual redesigned WPF rendering/theme/search/fullscreen checks:

    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release -- --design-tests

The full release verifier includes these checks with -Interactive. Main-window automation now uses the visible title BrainiacDeck Ultimate; internal executable/package/profile identifiers remain unchanged. Native field automation identifies labeled edit controls by both name and control type.

## Fresh-install verification

Run `dotnet run --project tests/BrainiacDesk.WindowsTests -c Release -- --fresh-install-tests` on Windows. It creates an isolated temporary profile and verifies that scripts, workspaces, Edge pins, clipboard items and activity start empty, with monitoring disabled. It never resets the normal user profile.
