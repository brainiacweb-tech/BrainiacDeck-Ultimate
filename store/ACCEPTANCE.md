# Release acceptance evidence

Record Windows build, account type, package family name, package SHA-256, display/DPI, action, expected result, actual result and supporting logs for every packaged check.

## Packaged lifecycle (requires trusted package and final identity)

1. On a clean Windows 11 standard-user test account, install the signed test MSIX, launch from Start, complete onboarding and open every advertised tool offline.
2. Save a script, workspace, preference, Edge note and managed screenshot; export a file to a user-selected folder. Record paths and hashes. Enable optional encrypted shelf persistence with harmless test data and exit normally.
3. Install a higher-version package with the SAME identity and publisher. Verify every saved item, preference and exported file. Clipboard monitoring must return disabled. Reject downgrade/identity-mismatch outcomes rather than reporting upgrade success.
4. Exit and uninstall using Windows Apps settings (or Remove-AppxPackage for that exact test package). Verify package registration and install directory are removed. Check package-owned redirected data separately from shared/unpackaged profile data; never delete originals or exports to force a clean result.
5. Reinstall and document actual retained/removed data. Compare policy wording with observed behavior.

## Accessibility and displays

- Keyboard only: all navigation, search, tool inputs, dialogs, dropdowns, focus visibility, Escape behavior and meaningful tab order.
- Narrator: control names/roles/state, script editing, error messages and operation results. Accessibility Insights for Windows: automated scan plus applicable manual assessment.
- Physical displays at 100%, 125%, 150%, 200%: resize/maximize/minimize; move between monitors with different DPI; confirm readable controls, working hit targets and no clipped fields.
- High contrast, reduced motion and Windows text scaling: verify each module, native dialogs, floating shelf, overlays and focus cues.
- Synthetic bitmap renders prove rendering at requested pixel densities only; they do not prove physical DPI-change behavior or screen-reader usability.

## Submission fields the developer must finalize

Partner Center identity, publisher display name, reserved display name, genuine support email/URL, hosted privacy-policy URL, pricing, territories, age-rating questionnaire, export-compliance answers and publication schedule. No values are invented. Publishing the listing or accepting developer agreements requires the account owner.
