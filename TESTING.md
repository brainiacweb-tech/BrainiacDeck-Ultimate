# Verification
Run both executable integration harnesses (not VSTest projects):

    dotnet run --project tests/BrainiacDesk.Tests -c Release
    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release

Then run `pwsh -File scripts/ui-smoke.ps1` on an interactive Windows desktop. It uses a unique temporary profile, verifies first-run setup, navigates the shell, opens/closes all tools, checks clipboard consent defaults, and verifies preferences across graceful exit/restart. Temporary profiles are retained for inspection. Normal profile preferences and clipboard contents are not modified by this smoke test.

`dotnet test` does not execute these harnesses. UI tests do not prove every control, hotkey, tray interaction or real capture path. See docs/QA-RESULTS.md and docs/READINESS.md for exact scope and pending manual tests.