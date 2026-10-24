# UI research and reusable patterns

Research date: 2026-10-23. These are source observations and design implications, not acceptance of any existing UI. Use the [UI workflow](ui-workflow.md) for execution. Paths below are resource-relative entry points to search in the current installation, not fixed resource IDs to copy.

## Evidence and limits

The local PAK source index was prepared on 2026-10-22 at revision `9b2fea665c0943b9bcf87ec2891bc011`. Its manifest records game `data/data007.pak` SHA-256 `5df8a2d49f4bb80def5c2f4c83cf63a27fb73a5f12b8527066762c5e695fd51a`; base-game script observations below came from that extracted archive. This is cached script source, not decompiled code or a live compatibility result. Authored-addon observations came from files on disk on the research date, not necessarily published mod versions.

No Reforger MCP tools were connected during this research. No new game/editor captures or interaction tests were performed. Existing concept briefs were read, but no current visual-quality verdict is asserted. The local wiki index pointer reported a failed capture (HTTP 403); direct web opens of Layout Editor, Mod Localisation and Widget Tooltip Setup also returned 403. Official-page text was available through web search retrieval; it is not represented as a successful live wiki capture. Recheck precise API/version questions in the connected index and installed sources.

## Official guidance

| Source | Relevant finding |
|---|---|
| [Layout Creation](https://community.bohemia.net/wiki/Arma_Reforger%3ALayout_Creation) (retrieval identifies oldid 370904) | Distinguishes passive displays, interactive menus and response dialogs. WLib buttons have component-owned text. The example binds close actions and removes their listeners on close. This supports choosing the surface and exit behavior before styling. |
| [Layout Editor](https://community.bistudio.com/wiki/Arma_Reforger%3AResource_Manager%3A_Layout_Editor?useskin=vector) | Documents clipping, text sizing, color inheritance, cursor handling, focus and navigation properties. Layout-file coordinates alone cannot establish the rendered result. |
| [Modular Button Component Tutorial](https://community.bistudio.com/wiki/Arma_Reforger%3AModular_Button_Component_Tutorial?useskin=darkvector) | Separates event/state-driven effects from the button's interaction. Includes mouse-to-focus behavior and event propagation controls. Inspect those settings before layering custom click or color logic over a component. |
| [Dialog Configuration Tutorial](https://community.bistudio.com/wiki/Arma_Reforger%3ADialog_Configuration_Tutorial) | Configurable dialogs compose title, message, content and footer controls from presets. Separate content layouts protect custom content from base-hierarchy changes. Reuse this machinery when it fits; do not require every screen to be a dialog. |
| [Input Manager](https://community.bistudio.com/wiki/Arma_Reforger%3AInput_Manager?useskin=darkvector) | Named actions abstract physical inputs; contexts govern which actions participate. The page contains unfinished sections, so exact flag behavior needs installed-source/runtime checking. |
| [Widget Tooltip Setup](https://community.bistudio.com/wiki/Arma_Reforger%3AWidget_Tooltip_Setup?useskin=darkvector) | Describes hover and gamepad-focus tooltips. Static tooltip invokers need identity checks and subscription cleanup. Supplemental help should not become another always-visible explanation panel. |
| [Mod Localisation](https://community.bistudio.com/wiki/Arma_Reforger%3AMod_Localisation), [String Editor](https://community.bistudio.com/wiki/Arma_Reforger%3AString_Editor) | Player strings use registered string tables and generated runtime tables. String Editor cautions against hardcoded text and assembling sentences from translated fragments. Test resolved text and fit in the actual UI. |

The simplification rules in the workflow are project design policy responding to the user's request, not claims that Bohemia mandates a particular palette, widget count or screen arrangement.

## Base-game implementation observations

Paths are beneath the archive's `scripts/Game/`:

- `UI/Components/WidgetLibrary/Button/SCR_ButtonTextComponent.c`, lines 25–30 and 88–97: attachment applies `m_sText` to the child; `SetText` updates both stored text and widget. Its color fields use `UIColors` roles. **Implication:** use the owning component and shared roles rather than competing child-text writes and individual color overrides.
- `UI/Components/ButtonComponents/ModularButton/SCR_ModularButtonComponent.c`, lines 540–610: click can focus the root; hover can move focus; return flags control event handling. **Implication:** nested controls need propagation and device-transition testing, not just a successful callback.
- `Editor/UI/Menus/EditorBrowserDialogUI.c`, lines 7–50: close clears placement state; menu open binds a named button, editor deactivation and input action; close removes deactivation/input listeners. **Implication:** dismissing UI must also restore the owning interaction state.
- `Editor/UI/Components/Common/SCR_DialogEditorUIComponent.c`, lines 130–188: input and gamepad subscriptions have teardown counterparts. **Implication:** verify repeated open/close, not merely the first opening.
- `UI/HUD/SCR_HUDManagerComponent.c`, lines 132–164: handler updates and throttled adaptive-opacity work are distinct. **Implication:** prefer event-driven discrete state without pretending all UI can avoid updates; HUD readability depends on the scene behind it.

These examples establish mechanisms, not that every base-game screen is a design ideal.

## Authored-addon inventory

All physical top-level directories in the Workbench addons root were enumerated. Counts below are `.layout`/`.ui` files from a file listing excluding Git and named Backup/backups directories; they are not screen counts. The `Lobby2IsolationProbe` symlink was not traversed again. `saves` had zero layouts. A zero does not rule out UI created in code or inherited from dependencies.

| Addon | Layout files |
|---|---:|
| gc-reforger-core | 10 |
| gc-reforger-missions | 1 |
| gc-reforger-units | 0 |
| GC-Suppression | 1 |
| Grays-Adjustable-Doors | 0 |
| Lobby2 | 60 |
| ReforgerLobbyCommunity | 65 |
| ReforgerLobbyCommunity - Copy | 65 |
| Test Bullshit | 4 |
| Test Bullshit3 | 0 |
| tilw-mission-framework | 1 |
| TTT | 1 |
| Voice-Volume-Slider | 1 |
| WL_Poppenhausen | 0 |

The similarly named community directories were inventoried separately; matching counts do not prove identical content or establish which is authoritative. Test-labelled folders are not automatically production examples. Detailed inspection was representative, not a full review of every file:

- **gc-reforger-core:** `scripts/game/Arty/GCC_ArtyMenu.c`, lines 32–78, uses `SCR_ComboBoxComponent` for shell choice, updates round/gun values, and removes the change subscription on close. A task-specific form can reuse standard controls without a new UI system.
- **Lobby2:** `UI/layouts/Lobby_MenuBar.layout` uses a repeated button prefab and component text for navigation. `UI/layouts/Admin/Lobby_AdminTools.layout` has scope, selection, action, summary and status areas; these are candidates for a per-state duplication audit, not proof of redundant visible text. `scripts/Game/UI/GC_AdminToolsMenu.c` pairs MenuBack registration/removal. Its `Art/References/` contains versioned admin-tools, admin-tools-redesign, admin-voice-channel, freeze-hud and native-chat media/briefs. Briefs distinguish concepts from rendered evidence and pending acceptance; use their current active versions, not earlier rejected versions.
- **ReforgerLobbyCommunity:** `PlayableSelector/UI/WaitScreen.layout`, lines 18–29, supplies a loading message. Waiting is a distinct UI state that needs honest feedback; its source alone does not prove it appears at the right time.
- **Voice-Volume-Slider:** `UI/layouts/VoiceSliderHud.layout`, lines 17–63, groups a range value, background/frame and slider. `scripts/game/modded/GRAY_SCR_VoNController_M.c`, lines 10–25, pairs action listeners. Small UI can remain focused; shared VON modifications still require active-mode isolation checks.
- **GC-Suppression:** `UI/layouts/HUD/ScreenEffects/ScreenEffects.layout` has vignette/flinch images; its script references screen-effect materials. This is noninteractive visual feedback, so it needs visibility/motion testing rather than a menu's primary-action rule.
- **tilw-mission-framework / TTT:** `UI/layouts/TILW_AOLimitLayout.layout` and `UI/layouts/TTT_AOLayout.layout` contain warning text, icon and timer. They offer a compact status pattern, but duplicate warnings from multiple active systems must be checked in play.
- **Test Bullshit:** `Menus/Inventory/InventoryMainTest.layout` inherits the base inventory layout. Inheritance is available; an experimental override is not an accepted style reference.

## Installed Workshop sample

The cached source listing contained 4,619 Workshop `.c` files in 282 addon directories; 611 matched a UI/HUD/Menu/Dialog/Lobby path filter. This is a search inventory, not all installed addons or complete UI coverage.

In `BaconLoadoutEditor_606B100247F5C709/data.pak`, `scripts/Game/BaconLoadoutEditor/GunBuilderUI_ListBox.c` lines 66–85 and 130–139 defer focus selection and create repeated item layouts with click subscriptions. `GunBuilderUI_MultifunctionSlotUIComponent.c` lines 281–288 updates a named label, and lines 410–438 include focus restoration. These illustrate explicit list/focus ownership. Do not copy the timing delay as a universal fix or infer visual usability without running the interface.

## Resulting strategy

Use existing Reforger presentation and input mechanisms, then simplify the information layered onto them. Decide each element's purpose and visibility before generating the concept. Preserve one theme and a clear journey through selection, action and result. Compare real captures with the saved concept and exercise actual input/recovery. This research supports that workflow; it does not certify or redesign the sampled mods.
