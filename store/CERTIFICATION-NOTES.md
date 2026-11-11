# Certification notes

BrainiacDeck Ultimate is a native WPF/.NET desktop productivity application. The runFullTrust capability is required for ordinary desktop file operations, user-selected screen capture, explicitly enabled clipboard listening, global keyboard shortcuts, topmost teleprompter/pinboard windows and native Windows integration. Normal application operation uses asInvoker and does not require administrator privileges.

No login or test account is required. Start with Get started. Each ordinary tool opens inside the main workspace; Close tool releases its session resources. Teleprompter opens its script editor first; Use script in overlay explicitly opens the floating reading window. Brainiac Edge intentionally opens a floating shelf. Clipboard monitoring is disabled unless the user enables it for the current session.

Capture and recording use Windows desktop APIs and do not bypass protected-content boundaries. Optional OCR uses Windows language components. Optional offline voice following requests microphone consent; it does not save audio. The recording editor exports silent MP4 clips. Manual workflows require explicit input/output folders and review. No scheduled workflows, cloud AI, cloud storage, login, advertising or telemetry are advertised.

Privacy controls remove retained activity and selected local settings. Original user files and user-selected exports are not removed by application cleanup. Uninstallation behavior and update preservation must be tested under the final package identity; no packaged lifecycle result is assumed from portable checks.

Package identity, publisher, public support/privacy URLs, age rating and availability must match the developer's Partner Center submission. Attach the actual final MSIX hash, WACK report and packaged install/update/uninstall evidence before submission.
