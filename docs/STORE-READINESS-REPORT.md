# BrainiacDeck Ultimate Store readiness report

## Current status — assigned identity configured

The owner supplied the actual Partner Center identity. It is now configured in `store/identity.json` and `src/BrainiacDesk.Package/Package.appxmanifest`:

- Package name: `KusiFrancis.BrainiacDeckUltimate`
- Publisher: `CN=A1F9B0A3-46F3-4CCB-B8E1-5137B14731AB`
- Publisher display name: `Kusi Francis`
- Public support: https://github.com/brainiacweb-tech/BrainiacDeck-Ultimate/issues
- Public privacy: https://github.com/brainiacweb-tech/BrainiacDeck-Ultimate/blob/main/PRIVACY.md

These public identity values are not credentials. No signing keys or passwords are included.

The assigned-identity MSIX was built, packed and unpacked successfully using MakeAppx. It is an **unsigned submission candidate**, not a certified release:

- File: `KusiFrancis.BrainiacDeckUltimate-0.2.0.0-x64.msix`
- Local output: `artifacts/store/assigned-identity/`
- SHA-256: `9B4FCE4E4F7F2294C5F3ED9C1CFEA934FB4BDDA08172472800A19C8CF42D9E7D`

All 11 new fresh-install checks passed: onboarding, empty projects/scripts/history/pins/shelf, genuine zero dashboard counts and disabled clipboard monitoring. No production seed data was found; test fixtures stay in isolated temporary profiles and are excluded from the published payload. Existing user data has not been erased.

The earlier full local release run passed 241 checks and published-app restart smoke tests. The source/build changes since that run configure the supplied identity, add repository documentation/assets and add the fresh-install test harness. Store preflight now passes identity/tool/manifest/icon checks. Public link availability must be confirmed after the repository push.

**Remaining:** local test signing with an appropriate trusted matching certificate, WACK on final binaries, final-identity installation/update/uninstall, physical DPI transitions and complete manual accessibility assessment. No Store upload, certification or publication has occurred. The layout-only artifact mentioned below is obsolete and must not be submitted.

---

The following is the historical preparation record before the identity was supplied; its missing-identity statements are superseded by the current status above.

# Earlier preparation record

Status: preparation and automated checks completed; final submission is BLOCKED on account identity/contact details and final-package acceptance. No Store certification or upload is claimed.

## Completed and evidenced

- Installed Microsoft's signature-verified Windows SDK 10.0.26100.9457 packaging/signing tools and Windows App Certification Kit.
- Added preflight, real-identity packaging, local-test signing, WACK reporting and listing/contact preparation scripts. Parser validation passed. Signing/WACK scripts have not been exercised with a final signed package.
- Built an 87,338,873-byte layout-only MSIX. MakeAppx schema packing and full unpack succeeded. SHA-256 and its placeholder publisher are recorded in artifacts/store/package-evidence.json. This artifact is explicitly LAYOUT-ONLY-NOT-FOR-SUBMISSION.msix and must not be uploaded or installed as a release.
- Release build passed with zero warnings/errors. 241 executable checks passed: 36 Core, 56 Windows/media, 28 Store UI, 33 single-window navigation, 26 redesign/theme, 27 Edge/native, 35 teleprompter/persistence. Published self-contained app UI smoke and full process restart passed. NuGet audit reported no vulnerable dependencies. This is portable/published-app evidence, not installed-MSIX evidence.
- The Store UI review initially found 22 unnamed controls. Added meaningful automation names to those inputs, dropdowns, sliders and results; the rerun reports zero unnamed controls among the reviewed control types/screens. One full-suite run encountered an unavailable test window; the harness now restores the review window before examining keyboard controls. The complete rerun passed.
- Actual application screenshots from an isolated profile are in artifacts/store/screenshots. Main light/dark screenshots are 1850x904 on this machine. No projects/history/statistics were invented. Synthetic rendering checks ran at 125%, 150%, 200%; physical DPI changes were not tested.
- Prepared en-US listing copy, full-trust capability/certification notes, privacy disclosures including shelf session behavior, a hostable privacy HTML page, genuine logo assets and acceptance procedures. Generated listing materials are drafts until actual support/privacy details are supplied.

## Still required

1. Exact assigned Partner Center PackageName, Publisher and PublisherDisplayName. Copy store/identity.example.json to store/identity.json and supply these values, display name, version, real support email and public HTTPS support/privacy URLs. No account identity or support contact was available in the repository. The source placeholder is deliberately retained, rather than inventing a publisher.
2. Build the final identity MSIX, sign a separate local test copy using an appropriate trusted matching certificate and run WACK on those exact binaries. No test certificate was created or trusted and no private key was exported.
3. Clean install, offline launch, higher-version update preserving real test content, restart and uninstall checks under the final package identity on a disposable test account/device. Do not infer cleanup from portable behavior; preserve original/exported files and observe Windows package-data redirection.
4. Physical mixed-monitor 125/150/200% DPI transitions; Narrator, contrast, Windows text scaling, high contrast and full keyboard/manual accessibility assessment. Automation-peer names and synthetic renders do not establish complete accessibility.
5. Publish the final privacy page at the genuine URL, verify support links, complete age rating, pricing, territories and compliance answers, review all evidence and submit through the developer's Partner Center account.

## Files

- store/identity.example.json: required account-specific inputs; no credentials requested.
- scripts/store-preflight.ps1: tool/identity/manifest checks.
- scripts/package-msix.ps1: schema-validated unsigned package build and evidence.
- scripts/sign-store-test-package.ps1: separate local test signing with a supplied certificate thumbprint; no keys in source control.
- scripts/test-store-package.ps1: trusted-signature check and optional real WACK report.
- scripts/prepare-store-content.ps1: final listing/contact output after real details exist.
- store/listing.en-us.json, store/privacy.html, store/CERTIFICATION-NOTES.md, store/ACCEPTANCE.md.
- artifacts/store/submission-preparation.zip: reviewable drafts, screenshots and evidence, excluding layout-only MSIX, executables and signing keys.

Official references checked November 11, 2026:
- https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements
- https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images
