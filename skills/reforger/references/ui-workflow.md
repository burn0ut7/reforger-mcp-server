# Reforger UI

Apply to all player-facing UI: menus, dialogs, HUDs, controls, labels and interaction states. Default to this style unless the user requests another direction. A wording or focus fix does not need a redesign.

## Visual style

The user's Reforgedmin Admin Tools screenshot (2026-10-28) is the visual reference: grouped sidebar navigation, small uppercase category labels, white icons and text, thin separators, translucent charcoal panels and a softened game scene behind them. Selected rows have muted yellow fill and a narrow brighter yellow edge; checked boxes use the same accent. Reproduce these traits, not its branding, admin content or exact columns. The image establishes appearance, not the underlying prefab or blur implementation.

- **Monochrome by default.** Black/charcoal surfaces, white primary text and restrained gray secondary text, borders and disabled controls. Subdued must still be readable.
- **Yellow means interaction state.** Reserve it for selected, checked, active or pressed controls, and explicit warning states. Do not color ordinary headings, role labels, decorative icons or primary actions yellow for emphasis. Navigation buttons are icon and text on a fully transparent idle surface, without an idle outline. Hover and keyboard/gamepad focus add a subtle neutral fill; selection adds muted yellow fill and a narrow bright yellow left edge. Reinforce state with a check, edge or shape.
- **Let the game show through.** Use translucent dark surfaces over a softly blurred world where appropriate. Keep foreground text and controls crisp; control background opacity separately. Avoid opaque card stacks, heavy shadows, ornamental gradients and saturated panels. HUDs should preserve gameplay visibility rather than blur the whole view.
- **Group with space.** Use small muted category headings, consistent rows, aligned icons and labels, generous gaps between groups and thin low-contrast separators. Separate navigation, content and detail only as needed; don't box every control. Keep silhouettes simple and consistent with native resources. Reuse one table-row prefab across related screens, including rows with embedded actions. Keep colors, headers, cell padding, row height and selection consistent; configure column widths for their content. Bound text to its cell, allow only modest shrinking to a readable minimum, and reveal clipped values through a native tooltip and selected-row details. Give row visuals distinct names from nested button visuals. Use one search across all displayed columns, combined with optional type filters; keep matches current as displayed values change.
- **Polish input fields.** Use the game font, consistent input sizes, off-white values, muted labels above and neutral placeholders. Keep label gaps small, inner padding even and single-line fields compact; reserve height for genuinely multiline content. Reuse field prefabs across forms. Searchable selectors accept typing in their header; avoid a second search field. Keep checkbox fills square and centered with equal inset, and verify their rendered geometry.
- **Keep words scarce.** Short titles, concise action labels and only the help needed to decide. Give each fact one home. Remove repeated subtitles, badges, instructions and technical IDs unless the task needs them. Prefer a familiar control that shows and changes state over a separate status label and button. Preserve essential scope, error and permission feedback.

## Reuse the game

Start with the [UI resource index](ui-resource-index.md) for useful leads, not a mandatory component list. When the user supplies a mod reference, inspect it first and trace the base-game resources it uses; otherwise start with the nearest shipped screen. Before designing a new control or surface:

1. Reuse the established catalogue and observed base-game source label; check `pak_status` when coverage is unknown. Search that source with `pak_search` for the nearest screen and relevant `.layout`, prefab or config; UI widget prefabs often live in `.layout` files, not entity prefabs.
2. Read the actual resource with `pak_read`, then follow relevant inherited parents, child prefabs, linked configs, styles, fonts, imagesets and materials. Inspect sizing, spacing, opacity, blur and state effects rather than stopping at filenames.
3. Use `pak_search_text` for the owning script components and representative callers, then `pak_read` for setup, runtime overrides and cleanup. Text search also covers readable layouts/configs; use widget classes, attributes or resource references to find examples, then read the relevant blocks.
4. Keep a compact note of the exact source, entry IDs/lines, revision and what will be reused or overridden. Distinguish stored values from resolved/live behavior; verify the result through MCP screenshots and interaction.

Use the [resource research workflow](workflows.md) for inheritance and encoding limits. [Research entry points](ui-research.md) are cached leads, not substitutes for reading the installed resources. Run PAK requests sequentially; the server allows one PAK operation at a time.

Reuse or derive from those resources and their input/state effects. Inspect how shipped UI implements transparency and blur; use the supported mechanism and verify its appearance. Do not guess resource IDs, invent a blur API or claim transparency alone produces blur. Report unavailable capabilities explicitly. Explain a concrete gap before introducing custom UI machinery.

Read component ownership: a component can overwrite a child widget's initial text or color. Keep presentation in layouts, presentation state in UI controllers and gameplay actions/permissions in their owning services. A disabled button is not an authority check. Follow [scripting conventions](scripting-style.md) for script changes.

## Design and build

1. Identify the player's task, essential information, next action and exit. Inspect the current UI and capture a baseline through MCP when available. Keep each visible element tied to the task; remove or defer the rest.
2. For new UI or substantial redesign, save and inspect a visual concept before production edits. Reuse a suitable supplied reference or saved concept; generate a task-specific concept when needed. Keep media outside runtime imports, normally `Art/References/<ui-name>/`, with a short brief identifying the active image, source, intended wording/states and deliberate departures. Preserve earlier versions, show the active image to the user and continue authorized work. Label illustrative backgrounds; a mockup is not game evidence. If required media is unavailable, report the gap before dependent visual work.
3. Use native sizing, wrapping and scrolling. Keep type and hit areas comfortable instead of shrinking content to fit. Preserve selection/focus during refreshes. Distinguish selecting a target from executing an action, and immediate changes from confirmed edits. Show pending, failure and confirmed results honestly; dispatch alone is not success.
4. Use named input actions and appropriate contexts. HUDs must not capture gameplay input. Pair subscriptions with teardown; closing releases owned widgets, focus and cursor and restores gameplay. Use stable target identities, refresh eligibility and preserve authority checks. Prefer event-driven updates over rebuilding unchanged widget trees. Use project string tables and check longer translations.

## Verify and review

Batch related UI edits into one live verification pass before claiming the UI verified: save and successfully Reload through MCP, inspect fresh compilation diagnostics, exercise each changed element, and capture its actual rendered states. Give a fresh independent UI reviewer the reference, current MCP screenshots and interaction/error evidence; fix findings and obtain approval. Source-only review, an old screenshot, or a blocked Reload does not pass. Fix the owning tooling blocker before continuing dependent UI work or claiming completion.

Follow [Workbench guidance](workbench-workflow.md); game/editor interaction and captures use Reforger MCP. Keep the current process/project open and use Reload. Unsupported input or capture remains an explicit verification gap, not a reason to use another backend.

Define relevant pass criteria, load the changed UI in-game, then exercise the real journey, capture actual UI, compare with the exact active reference, fix and recapture. Check:

- Composition, category spacing, concise wording, monochrome defaults and yellow state cues; legibility and transparency/blur over bright and dark scenes.
- Supported viewport/UI scales and input devices; long text, empty and scrolling lists, focus versus selection, back/cancel and reopening.
- One activation produces one intended effect; pending/error recovery, permission changes and relevant client/server state. Reopening must not duplicate listeners or actions. Shared overrides require mode-isolation checks.

Keep original captures and relevant state/log receipts with build, mode, viewport and reproduction steps. Screenshots establish appearance; actual input and resulting state establish behavior. Report missing coverage honestly. Measure against a comparable performance baseline when changing UI update, frame or loading costs.

For deterministic tooltip activation tests through MCP, an optional test addon can report cell-center movement with the installed `WidgetManager.ReportMouse(x, y, menuRoot)` API, then report leaving the cell. Observe the layout-attached tooltip appearing and dismissing; do not call `Show` or inject its hovered widget. This tests native widget dispatch, not physical mouse or OS focus handling. Capture while the tooltip is actually visible.

For substantive work, obtain independent review under AGENTS.md using the request, changes, active reference and actual evidence, including limitations. Resolve feedback and return to the same reviewer until approved. Handoff briefly with final captures, tested behavior and remaining gaps.
