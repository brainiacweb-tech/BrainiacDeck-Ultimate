# Store preparation — pending
The build is not ready for submission. Reserve and verify the product name and publisher identity in Partner Center. Create a Windows Application Packaging Project referencing the WPF executable; target x64 Windows 11. Replace publisher and identity with assigned values, generate original assets, audit dependency licenses, and use only necessary capabilities. Never commit signing credentials.

Build a self-contained Release payload, produce and sign MSIX, then test install, launch offline, upgrade with retained preferences, uninstall, and run the Windows App Certification Kit on the actual package. Review current Microsoft Store policies and validate every advertised feature. Publish privacy and support URLs before submission. Do not advertise planned utilities in the listing.

References: https://learn.microsoft.com/en-us/windows/msix/desktop/vs-package-overview and https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-packaging-dot-net