# Verified results — 2026-11-09
Environment: Windows 11 x64, OS build 22631, .NET SDK 10.0.403 installed locally. All results below are from actual commands run in this workspace. This is not a certification report.

Test | Environment | Result | Evidence | Remaining issue
--- | --- | --- | --- | ---
Release solution build | Windows 11 / .NET 10 | Passed | dotnet build BrainiacDesk.slnx -c Release; 0 warnings, 0 errors | MSIX project needs separate VS/SDK tools
Core/storage/file/workflow checks | Same | Passed: 19 | dotnet run --project tests/BrainiacDesk.Tests -c Release | Full UI operations and filesystem race hardening pending
Image/PDF/annotation/command checks | Same, STA Windows harness | Passed: 26 | dotnet run --project tests/BrainiacDesk.WindowsTests -c Release | Large-input and codec fuzzing pending
Self-contained x64 publish | Windows 11 | Passed | dotnet publish ... -r win-x64 --self-contained true -o artifacts/portable | Unsigned development payload
Portable executable UI smoke | Windows 11 interactive desktop | Passed | scripts/ui-smoke.ps1 -Executable artifacts/portable/BrainiacDesk.App.exe | Does not exercise every tool control
First-run onboarding and shell navigation | Isolated temporary profile | Passed | UI Automation invoked Get started, Settings save, Help, About, Dashboard | Accessibility audit pending
All ten tool windows | Portable executable | Passed | Converter, Shelf, Teleprompter, Commands, Shortcuts, File Toolkit, Workflows, Workspaces, Capture, Privacy open/close | Full operation UI acceptance pending
Clipboard monitoring defaults | Isolated profile | Passed | Consent checkbox is Off when shelf opens | Real clipboard content/listener test pending
Full process restart | Portable executable | Passed | Saved Light preference restored after graceful exit and fresh launch | Upgrade persistence not tested
Graceful app exit | Portable executable | Passed | Both process exits completed successfully | Confirmation dialog and tray interaction pending
NuGet vulnerability audit | Current configured NuGet source | Passed | dotnet list src/BrainiacDesk.App package --include-transitive --vulnerable: no vulnerable packages reported | Not a full security/license audit
MSIX installation/upgrade/uninstall/WACK | Windows 11 | Pending | No MSIX generated; MakeAppx/SignTool not found in checked tool locations | Install tools, assign publisher identity, package and verify
DPI/multiple monitors/Narrator | Windows 11 | Pending | No validated report | Manual testing required
Actual capture / clipboard / hotkey conflicts / overlay timing | Windows 11 | Pending | Pure image operations and tool-window startup tested | Native integration acceptance still required

## Detailed executable harness coverage
Core: defaults, restart storage, updates, concurrent saves, storage errors, SHA-256/SHA-512 known vectors, size/hash/byte duplicate verification, rename/undo/collision, ZIP round trip/traversal/malicious archive/cancellation, workflow dry run/permissions/recursive roots and copy/hash/ZIP execution.

Windows: five image outputs with resize, output protection, corrupt input, unsupported output, cancellation/no output, image-to-PDF, text pagination, PDF extract/reorder/range parsing/invalid range/merge/rotation, text normalization/casing, temporary output cleanup, screenshot crop/opaque redaction, four annotation tools, deterministic command matching.

## Resolved findings
- Initial SQLite 2.1.11 dependency chain failed NU1903; updates removed the reported advisory.
- UI smoke originally searched only desktop-root children and missed owned WPF tool windows. Searching descendants corrected the harness; the complete portable-executable run then passed.
- Initial launch timeout was expanded from 10 to 40 seconds. This does not constitute a startup benchmark.
- Closing active tool operations is now blocked until they finish or are cancelled, preventing ordinary window-close interruption of file work.

No performance numbers, Store approval, WACK success, full security review or completion of every original feature is claimed. See READINESS.md for the remaining specification gaps.

## Interface redesign verification
Rendered and inspected actual dark/light dashboard and converter previews. Corrected clipped text fields and dark-theme text inheritance. Release build passed with zero warnings/errors. Republished the final self-contained executable and passed the full isolated-profile UI smoke check, including all ten tool windows, converter dropdown expansion/collapse, saved settings across process restart and graceful exit. New styling does not imply a completed accessibility or multi-DPI audit.

## Raleway, icons and cards verification
Verified packed Raleway glyph faces for Normal, Medium, SemiBold and Bold; all resolved to embedded Fonts resources. Rendered and inspected dark/light dashboard and converter previews. Published the self-contained app and passed the isolated-profile smoke test, including all four dashboard-card actions, all ten tool windows, converter dropdown expansion/collapse, preference restart and graceful exit. The first card test missed a control after child-window close; adding bounded owner-control lookup retries and a short close-settle interval resolved the harness failure. Confirmed published Fonts/OFL.txt and ThirdPartyNotices/Lucide-LICENSE.txt are present. No runtime downloads or system font installation are required.
