# Wiki entry points and workflows

Use this curated shortlist to find a useful starting guide, then search beyond it for the requested feature, API, or asset. Article titles below are exact suffixes of `Arma Reforger:`; italic text identifies useful sections. Read on demand, rather than treating the list as a required sequence or complete coverage.

## Finding a page

For a known topic, use e.g. `wiki_search(query="Enforce Script Syntax", kind="title", limit=5)`. Select the matching title and use its returned `pageId` and the search's index `revision` with `wiki_read`. Browse passages to locate the relevant section, then use `view="section"` with its `passageNumber` for context and warnings. Follow `nextOffset` or the tool's truncated-passage guidance when necessary. Resolve IDs from the current index rather than embedding machine-specific paths or old page IDs here.

Use `research_mod` when the question spans topics rather than naming a guide. If the index is unavailable, report that state; searching does not prepare or refresh it.

## Category routes

Search with `kind="category"`. Paths in the first column follow `Category:Arma Reforger/Modding`; `/` means that root category. Child category names in the descriptions extend their row's path (e.g. `/Scripting/Guidelines`). Category filters cover direct membership only, so follow subcategories explicitly. Category text can itself contain guidance, not just article lists.

| Path | What it helps find |
|---|---|
| `/` | Broad modding launchpad: project creation, content disciplines, publishing, and server hosting. |
| `/Scripting` | Language overview; `Guidelines` for language, lifecycle, and performance; `Tutorials` for components, entities, actions, configuration, and multiplayer. |
| `/Scenario` | `Guidelines` for Scenario Framework and tasks; `Tutorials` for game mode, respawn, and complete scenario examples. |
| `/Official Tools` | Editor roles and documentation; `Resource Manager Viewports` for config, layout, material, model, and texture editors. UI documentation does not imply an MCP operation exists. |
| `/Assets` | `Guidelines` and `Tutorials` for asset creation and integration; `Weapon Creation` for the weapon pipeline. |
| `/Terrains`, `/Audio`, `/Game Master` | Terrain/navigation, sound, or editable-entity workflows when the feature reaches those systems. |
| `/Guidelines/Systems` | World systems, ordering and periodic work; read *Current Limitations* against the installed version. Leads to persistence. |
| `/Scripting/Workbench/Tutorials` | Editor plugins/tools and WorldEditorAPI usage when authoring an editor extension. |

## Basics on demand

Pick the row matching the uncertainty, rather than loading every basic guide for every task. Use Reforger-specific documentation; similar C# or DayZ syntax does not establish Reforger behavior.

| Question | Saved wiki entry points and useful sections |
|---|---|
| How do I start and debug a small script? | **Scripting First Steps** introduces running examples. **Script Editor**: *Navigation > Searching* for symbols/references; *Debugging* for breakpoints, watch, callstack, console, and VM exceptions. |
| Where is the language reference? | **Enforce Script Syntax** is a hub. **Scripting: Values** covers identifiers, types/collections, and casting; **Scripting: Operators** covers expressions/precedence; **Scripting: Keywords** explains language modifiers, including engine declarations. |
| How do classes and script modifications work? | **Object Oriented Programming Basics** introduces classes/members. **Object Oriented Programming Advanced Usage**: *Modding* and *Precedence* explain `modded`, same-module placement, and dependency order. **Scripting Modding** applies this to existing gameplay code. |
| When does behavior run? | **Entity Lifecycle**: *Lifecycle* maps callbacks and their event-mask/condition dependencies. **Entity Activeness** explains FRAME/ACTIVE behavior; preserve its version qualifications. |
| Who keeps objects alive? | **Scripting: Automatic Reference Counting** explains strong/weak references and cycles. **Resource Usage**: *Good Practice* shows why retaining a BaseContainer alone does not retain its owning Resource. |
| How should scripts be structured and optimized? | **Scripting: Best Practices** gives conventions; **Scripting: Do's and Don'ts** provides paired examples. **Scripting: Performance**: *Performance Enemies*, *Specific Issues*, and *Benchmark* help locate expensive work and evaluate changes. |

## Projects, resources, and configuration

| Need | Saved wiki entry points and useful sections |
|---|---|
| Establish an addon and its dependencies | **Mod Project Setup**: *Project Creation*, *Managing Projects*, and *Troubleshooting*. **Directory Structure** maps folders and script modules; its conventions are not all engine requirements. |
| Identify files and their owning tools | **File Types** maps extensions to purpose/editor. **Resource Manager**: *Resource Browser* covers registration, resource names/GUIDs, references, and import; **World Editor** covers hierarchy, object properties, and Play. |
| Choose the scope of a data change | **Data Modding Basics**: *Content Moddability Overview* and its table/footnotes distinguish replacement, override, duplication, and inheritance by resource type. |
| Understand reusable versus placed content | **Prefabs Basics**: *Glossary*, *Creating inherited prefab*, *Editing entity prefab properties*, and *Prefab overriding* explain prefab types, instance changes, propagation, and saving. |
| Inspect or edit structured resource data | **BaseContainer Usage**: *Structure > Inheritance*, *Usage > Read*, and *Usage > Update > WorldEditorAPI*. Direct IEntitySource changes can remain only in memory; follow the editor API guidance for persisted authoring. |
| Define and load custom configuration | **Create a Config Class**: *Make It Root* and *Example* cover config roots and exposed attributes. **Scripting Conf File Usage** compares *ResourceName Approach* with *Object Approach*. |
| Share data across many instances | **Prefab Data** contrasts class/prefab data with instance fields, including memory/access tradeoffs; useful when many entities repeat the same configuration. |

Check dependencies and game/Workbench branch compatibility. Distinguish a new inherited variant, an override affecting an existing resource, and a placed instance's local values. Use resource identities returned by the tools; do not invent GUIDs.

## Scripts and interactions

Use **Scripting Modding** for extending existing behavior, **Create a Component** or **Create an Entity** for new behavior, and **Creating a User Action** with **Action Context Setup** for player interactions. Gameplay components/actions belong in `scripts/Game`; an editor-visible component needs its matching `ComponentClass` declaration. A compiled action still needs its context and ActionsManager configuration.

For event-driven behavior, **Event Handlers** explains event patterns and **ScriptInvoker Usage** covers subscriptions and typed signatures. Read their cautions about direct untyped ScriptInvoker use and unnecessary allocation. Check callback/event-mask requirements and subscription lifetime; preserve existing project conventions.

## Scenarios and game flow

Start with **General Game Mode Setup** and **Respawn Setup** to connect the game mode, faction/loadout managers, spawn logic/handlers, and player-controller behavior. Consider entry/selection, spawn and respawn, PREGAME/GAME/POSTGAME, and the end condition as relevant to the requested mode.

**Scenario Framework** and **Scenario Framework Setup Tutorial** explain configuration-driven missions using Areas, Layers, Slots, tasks, and actions. The tutorial provides a small complete example: player spawn, move objective, activation of a randomized destroy objective, then EndMission. Include the mission header and verify the scenario is discoverable and playable from the game, where requested. Choose existing world/subscene resources deliberately; terrain creation is a separate workflow.

For direct task integration, **Task System Usage** assigns high-level mutations to `SCR_TaskSystem` and reads to `SCR_Task` public getters, rather than manipulating `SCR_TaskData`. For AI/world dependencies, consult **New Terrain Setup** and **Navmesh Tutorial** as needed.

## Multiplayer and saved state

Read **Multiplayer Scripting** for authority versus ownership, RPC routing, streaming, and replicated entities. Embedded Play alone does not establish dedicated-server behavior; a broadcast does not imply delivery to a client without the relevant streamed proxy.

Read **Persistence System** when gameplay must survive saves/restarts. Follow tracking, serializer/configuration, storage scope, and asynchronous restoration together. Verify restored behavior, not merely that a save request returned.

## Assets, UI, and integration

Choose the relevant **Weapon Modding**, **Car Modding**, **Prop Creation**, **Faction Creation**, or asset-creation guide. Trace the required model/material/audio configuration into prefabs and gameplay use. **Entity Catalog**, **Asset Browser Mod Integration**, and **Game Master: Editable Entities Configuration** cover registration/discoverability. For player-facing UI, follow the [required UI workflow](ui-workflow.md), including UI behavior changed through scripts. It routes to layout, input, component, and localisation documentation as needed. An asset existing on disk does not establish that the player can find or use it.

## Testing and delivery

Use **Script Profiling**, **Autotest Framework**, and the feature's own testing sections when they fit the task. Test integration and recovery proportionally, including persistence of authored changes and multiplayer effects where relevant.

**Mod Publishing Process** covers bundling, metadata, visibility, and separate stable/experimental Workshops. Its publishing working directory must differ from the addon source directory. The MCP server does not currently supply mod bundling/publishing automation; documentation of a UI or CLI command is not evidence that an MCP capability exists.

These cues were researched from the local official-wiki browser capture of 2026-10-20. The capture is partial, and images/external examples may only be linked. Read current capture metadata and preserve warnings: some guides contain obsolete subsections, unfinished material, or historical migration advice. Do not generalize old retry/delete instructions or treat uninspected media as verified evidence.
