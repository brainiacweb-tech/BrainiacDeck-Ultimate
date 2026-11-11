# Microsoft Store submission preparation

## Implemented preparation

- Windows SDK MakeAppx, SignTool and Windows App Certification Kit installed from Microsoft's signature-verified SDK installer.
- scripts/store-preflight.ps1 records identity/tool/manifest/icon checks.
- scripts/package-msix.ps1 reads the actual assigned identity from store/identity.json, generates a staged manifest without changing saved data, publishes self-contained win-x64, packs and unpacks with schema validation, and records package SHA-256/evidence.
- An explicit -LayoutValidationOnly mode retains the placeholder source manifest and produces LAYOUT-ONLY-NOT-FOR-SUBMISSION.msix. This is layout evidence only, never a submission or installation candidate.
- scripts/test-store-package.ps1 records package/WACK evidence when a real trusted local test package exists.
- scripts/prepare-store-content.ps1 generates listing and contact materials after real details are configured.
- store/listing.en-us.json, store/privacy.html, capability notes and lifecycle/accessibility acceptance steps are available.
- --store-review renders actual isolated application views, synthetic DPI densities, button names and basic keyboard traversal, recording all remaining accessibility findings.

## Account-dependent steps

The assigned identity supplied by the owner is checked into store/identity.json and the package manifest. It uses KusiFrancis.BrainiacDeckUltimate, the exact assigned publisher CN, and publisher display name Kusi Francis. Public support uses the repository Issues page; the public privacy URL points to PRIVACY.md on main. A support email is optional when a genuine support URL is provided. These public identity values are not credentials; private keys and passwords remain excluded from source control.

The assigned-identity unsigned MSIX has passed MakeAppx pack/unpack validation. Final installed-package acceptance and WACK are still pending.

Run:

```powershell
./scripts/store-preflight.ps1
./scripts/prepare-store-content.ps1
./scripts/package-msix.ps1
```

Store re-signs accepted MSIX submissions. Local installation and WACK testing require a separately trusted test signature matching the publisher. Do not purchase a certificate solely on the assumption it is required for Store submission. Do not trust arbitrary certificates or place private keys in source control.

On a disposable standard-user Windows test account/device, run installation/update/uninstall acceptance for the FINAL identity. Use the same identity and publisher with a higher package version for update testing. Never delete user originals to force cleanup success. Record actual package registration, storage redirection and retained/exported files.

Run WACK on the final signed test package and retain its report:

```powershell
./scripts/test-store-package.ps1 -Package '<signed-final-package.msix>' -RunWack
```

A generated report is not automatically a pass. Review all WACK failures, then rerun on corrected final binaries. Physical monitor DPI transitions, Narrator, high contrast and text scaling are separate manual acceptance steps in store/ACCEPTANCE.md. Synthetic image rendering does not replace them.

Upload only the real final identity package and approved listing assets through the account owner's Partner Center. Complete age ratings, availability, support/privacy links and full-trust capability justification. No Store upload, account agreement, certification result or publication is implied by this preparation.

Official references:
- https://learn.microsoft.com/en-us/windows/apps/publish/get-started
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements
- https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images
