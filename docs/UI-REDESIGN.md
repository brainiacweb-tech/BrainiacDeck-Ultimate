# UI redesign
The dashboard now uses grouped navigation and four actionable tool cards, with secondary organization tools below. Settings and onboarding use clear sections. Shared WPF resources style buttons, fields, selectors, lists, focus states, surfaces and scrollbars in both themes.

Tool windows have fixed headings/status areas and compact action rows. The converter now places its file queue/results beside output options, with preview, presets and PDF page tools progressively disclosed. Conversion engines and privacy defaults were preserved.

Visual review uses the actual WPF controls rendered to PNG, not mockups. Run:

    dotnet run --project tests/BrainiacDesk.WindowsTests -c Release -- --visual-review

Previews appear in artifacts/ui-review: dashboard-dark.png, dashboard-light.png and converter-dark.png. The desktop here constrains windows to approximately 720p; text clipping and primary-control visibility were checked at that size. This does not replace multi-DPI, Narrator or full accessibility testing.

The UI smoke script covers first-run navigation, all ten utility windows, the styled converter dropdown, and preferences across restart. Utility coordinators still need individual workflow-focused UX refinement beyond the shared styling.
