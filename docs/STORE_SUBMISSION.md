# Microsoft Store submission — pending
The source includes src/BrainiacDesk.Package/BrainiacDesk.Package.wapproj, a manifest and original logo assets. It has not been built or validated by MSIX tooling. A self-contained portable development payload is available; it is not an installer or a Store package.

1. Finish the acceptance gaps in READINESS.md and perform security/accessibility/license reviews.
2. Verify trademarks/name availability and reserve the product in Partner Center.
3. Replace the manifest identity/publisher placeholder with assigned values and confirm package versioning.
4. Install Visual Studio packaging tools and Windows SDK. Build the .wapproj or use scripts/package-msix.ps1 with MakeAppx. The script refuses the placeholder publisher.
5. For local sideload testing, sign with an appropriate trusted test certificate on a test device; never commit keys. Store MSIX submissions do not require purchasing a CA certificate: Microsoft re-signs certified packages for distribution.
6. Test clean install, offline launch of every advertised tool, settings/data retention across upgrade, startup/exit/listener disposal and uninstall. Confirm behavior of user-exported files separately from package-owned data.
7. Run Windows App Certification Kit: open the kit, select validation of a Windows Store app/package, choose the installed package, run all applicable tests and retain the generated report. Review every failure; rerun on the actual final package. The kit is part of the Windows SDK tooling. No WACK result exists for this build.
8. Publish genuine support and privacy URLs; finalize listing copy/screenshots using STORE_LISTING.md. Do not advertise deferred formats, OCR, scheduled/background workflows or advanced features.
9. Complete Partner Center age ratings, pricing/free availability, territories, capabilities, privacy/support links and certification notes. Submit only verified packages; review results before public release.

Official references checked for this work:
- https://learn.microsoft.com/en-us/windows/msix/desktop/vs-package-overview
- https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store
- https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-certification-process
- https://learn.microsoft.com/en-us/windows/apps/publish/store-policies

No Microsoft approval, signed MSIX, installation success or name reservation is claimed.
