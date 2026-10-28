# UI resource index

A starting guide, not an approved-only list. Choose resources for the player's task, inspect their current definitions and adapt deliberately. Packed `.layout` files can themselves be widget prefabs; being inside a PAK does not make readable resource text decompiled code.

## Coverage

The entries below were read through Reforger MCP on 2026-10-28 at catalogue revision `af6068cb44aa4c0db613213faa82a050`. They are stored resource definitions, not live appearance or interaction tests. Further research stopped on 2026-10-28 after catalogue preparation timeouts; recheck `pak_status` before extending the index. This is a small verified starting set, not a complete base-game inventory.

Reforgedmin Admin Tools 1.0.57 (`6A6EA6AD906D8C12`) checkbox source was read on 2026-10-28 through the same MCP PAK backend with an isolated configured-source catalogue (revision `f29e551559714c8fa5a0f838634846c6`). `data.pak/UI/layouts/HSM_Checkbox.layout` uses native `SCR_ButtonTextComponent`, a 22px outlined square and a yellow `Tick` inset by 5px. `scripts/Game/HSM_UiTools.c` lines 614–673 creates it and controls tick visibility; `HSM_AssetSpawner.c` lines 563/648 creates and updates the budget checkbox. This is a custom composition of native widgets, not the stock toolbox checkbox. Reuse the pattern without adding a mod dependency; keep confirmed state in the owning controller. Source/caller inspected, not runtime-tested. Broader addon review remains pending.

## Inspected resources

Paths are relative to the named source archive. Search the current catalogue for the path to obtain an exact entry ID; do not construct resource GUIDs from these names.

| Need | Resource | Observed composition / usage | Reuse notes |
|---|---|---|---|
| Plain background blur | `UI/layouts/WidgetLibrary/BaseElements/WLib_Blur.layout` | Base game `data/data010.pak`, lines 1–9: `BlurWidgetClass`, aligned to fill, intensity `0.4`. No consuming screen traced yet. | Starting point for the blur layer; does not by itself specify the menu's tint, contrast or input behavior. |
| Readable translucent background | `UI/layouts/WidgetLibrary/BaseElements/WLib_ReadabilityBackground.layout` | Same archive, lines 8–39: dark shadow/panel opacity `0.3`/`0.5`, separate blur intensity `0.1`, smooth borders. Referenced by the modular icon button below. | Transparency and blur are separate. Inspect the caller's overrides and visibility before copying stored defaults. |
| Icon button | `UI/layouts/WidgetLibrary/Buttons/WLib_ModularButton_Icon.layout` | Same archive, lines 5–13, 34–50 and 63–80: modular-button and dynamic-icon components, inherited readability background, `Glow` and `Icon` images using game imagesets. | Background starts hidden; blur is overridden to `0.4`. Follow the linked config for states/events. A reusable control, not yet a traced end-to-end screen example. |
| Button states and feedback | `Configs/WLib/WLib_ModularButton_Icon.conf` | Base game `data/data007.pak`, lines 1–42: linked icon/background color effects, confirm/focus/hover sounds, toggle capability and focus-on-mouse-enter. Used by the icon-button layout above. | Native stored hover/focus colors are orange. Adapt state colors to the requested monochrome/yellow style through the owning effect configuration; inherited effects and script behavior still need inspection. |

Reforgedmin input example: `UI/layouts/HSM_NumberField.layout` (same addon revision above), lines 1–47, composes a vertical label/input field: 15px muted label, 6px label gap, 16px value, 14px horizontal/10px vertical inset and black background alpha 0.35. `HSM_PlayerMonitoringTab.c` `BuildSetting` binds the label and native `EditBoxWidget`, while `FillRowCell` aligns columns. Adapt scale and contrast deliberately; these are source values, not verified sizes on every viewport.

## Find the next example

Use the observed base-game source label from `pak_status`. These are search leads, not verified prefab names or guarantees of suitability:

| Family | Resource path search terms | What to trace |
|---|---|---|
| Buttons and navigation | `Button`, `Navigation`, `Tab` | Click versus toggle, focus/hover/selected effects, sounds, parent event handling. |
| Text entry and search | `Edit`, `Search`, `Input` | Owning component, changed versus submitted events, validation, focus and gameplay input suppression. |
| Boolean and exclusive choices | `Check`, `Toggle`, `Radio` | Stored checked state, label hit area, disabled behavior and single activation. |
| Lists and choices | `List`, `Combo`, `Dropdown` | Row prefab, selection identity, refresh, scrolling and focus retention. |
| Numeric adjustments | `Slider`, `Spin` | Range/step, value display, commit timing and device input. |
| Icons and hints | `Icon`, `Tooltip`, `Hint` | Imageset and named image, sizing/tint, label/help, device-specific hints. |
| Surfaces and dialogs | `Background`, `Blur`, `Dialog` | Layering, opacity, inherited resources, input context, dismissal and cleanup. |

Search one term at a time: multiple terms in `pak_search` are AND conditions. Start with `kind="layout"`, then follow observed links into configs, imagesets, styles and scripts. `pak_search_text` searches scripts only; it cannot find all layout-to-layout references. Follow readable resource links and targeted screen layouts, and record where caller coverage is incomplete.

## Add an entry only after reading it

Keep each entry compact: purpose, source/version or catalogue revision, exact path and useful lines, owning component/config, one inspected consumer when found, state behavior, customization points and caveats. Mark whether it is resource-read, caller-traced or runtime-tested; do not upgrade those evidence levels by inference.

For a supplied mod, trace its screen layout → control prefab → inherited game resource/config → script callbacks and lifecycle. Then find a shipped screen or contrasting mod using the same mechanism. Record which visual choices are local overrides and which behaviors come from the shared control. Keep observations and recommendations distinct; a good appearance does not certify its validation, permissions or cleanup.

Use [the research workflow](workflows.md) for the full source trace and [the UI workflow](ui-workflow.md) for design and live acceptance. Keep this index in the skill; project AGENTS.md does not need prefab lists or UI style rules.
