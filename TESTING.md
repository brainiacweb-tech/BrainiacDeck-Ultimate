# Testing
Run `dotnet run --project tests/BrainiacDesk.Tests -c Release`. This is a dependency-light executable integration harness, not a VSTest project; `dotnet test` does not execute these checks.

On an interactive Windows desktop run `pwsh -File scripts/ui-smoke.ps1` after a Release build. It launches its own process, saves default preferences to the normal development app profile, exercises navigation, and terminates only that process. Do not run against a profile containing important preferences. It does not test graceful exit or tray disposal.

See docs/QA-RESULTS.md and docs/READINESS.md for evidence and pending manual checks.