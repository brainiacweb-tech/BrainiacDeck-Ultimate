# Recorded verification — 2026-11-09
Environment: Windows 11 build 22631, x64, locally installed .NET SDK 10.0.403.

Test | Environment | Result | Evidence | Remaining issue
--- | --- | --- | --- | ---
Release solution build | Windows 11 / .NET 10 | Passed | dotnet build: 0 warnings, 0 errors | No runtime certification implied
Settings defaults | Same | Passed | Console harness PASS | UI onboarding unverified
Settings restart persistence | Same | Passed | New SettingsStore reads saved values | Full process restart pending
Settings update | Same | Passed | Dark theme persisted after Light | UI theme rendering pending
Concurrent saves | Same | Passed | 20 saves then equality check | Cross-process contention untested
Storage failure | Same | Passed | Invalid parent path propagates IOException | Startup dialog manual test pending
Native window/navigation | Interactive Windows desktop | Passed | UI Automation invoked Settings, Save preferences, Help, About, Dashboard | Tray and graceful exit pending

Initial build was blocked by NU1903 for SQLitePCLRaw.lib.e_sqlite3 2.1.11. Microsoft.Data.Sqlite was updated to 10.0.14 and SQLitePCLRaw.bundle_e_sqlite3 to 3.0.5. Subsequent restore/build reported no vulnerability warnings. This is not a complete security audit.

The first UI smoke attempt timed out at 10 seconds; the retry with a 40-second allowance passed. No startup performance claim is made. The smoke harness forcibly stops only its own process in cleanup, so shutdown lifecycle remains pending.