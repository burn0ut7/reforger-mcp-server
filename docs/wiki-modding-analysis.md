# What the captured Reforger wiki adds to mod making

## Captured evidence

On 2026-10-20, ordinary browser navigation from the [Modding category](https://community.bistudio.com/wiki/Category:Arma_Reforger/Modding) captured 298 page visits, consolidated into **292 distinct pages: 250 articles and 42 categories**. The local index contains **1,066 code blocks**. Source page IDs, observed revision links, capture times, complete rendered HTML, article content, categories and links are saved.

The captured page graph contains **3,542 distinct link URLs**, including **801 `enfusion://` links** and **275 external HTTP(S) links**. Counts include section URLs and are not counts of independent documents. The link audit found zero unresolved in-scope links in the discovered graph, after accounting for redirects. This establishes closure of this traversal, not coverage of every Reforger page on the site.

The API attempt remains separately recorded as failed. The browser corpus is an explicitly selected source imported into the same C# wiki store. Search and reading now work offline; another browser capture is needed to check upstream changes. Images/video/download binaries and external sites are linked but not downloaded.

## Highest-value documentation

| Mod-making need | What the captured documentation contributes | Practical use |
|---|---|---|
| Set up a reliable project | [Mod Project Setup](https://community.bistudio.com/wiki/Arma_Reforger:Mod_Project_Setup) covers game data, dependencies, writable project locations and matching Workbench/game branches. | Diagnose setup and dependency problems before changing scripts. |
| Modify existing assets safely | [Data Modding Basics](https://community.bistudio.com/wiki/Arma_Reforger:Data_Modding_Basics) distinguishes GUID-based resource overrides, script paths, `modded` classes and prefab inheritance. [Prefabs Basics](https://community.bistudio.com/wiki/Arma_Reforger:Prefabs_Basics) explains entity/component/config prefabs and saving behavior. | Choose the correct owning resource and modification mechanism; avoid treating every change as a script edit. |
| Add usable scripted behavior | [Create a Component](https://community.bistudio.com/wiki/Arma_Reforger:Create_a_Component) and [Creating a User Action](https://community.bistudio.com/wiki/Arma_Reforger:Creating_a_User_Action) connect scripts to their directory, editor-visible declarations, authored components and compile/reload steps. | Return a complete implementation checklist alongside an example, including the required prefab/editor work. |
| Make multiplayer behavior correct | [Multiplayer Scripting](https://community.bistudio.com/wiki/Arma_Reforger:Multiplayer_Scripting) distinguishes authority from ownership and explains streaming, RPC routing and insertion rules. Its broadcast discussion is scoped to machines where a proxy is streamed. | Check authority, ownership, locality and streaming before proposing replication/RPC code. A broadcast name alone does not establish delivery to every client. |
| Avoid lifecycle and memory bugs | [Entity Lifecycle](https://community.bistudio.com/wiki/Arma_Reforger:Entity_Lifecycle) documents event masks, callback timing and destructor restrictions. [Automatic Reference Counting](https://community.bistudio.com/wiki/Arma_Reforger:Scripting:_Automatic_Reference_Counting) explains strong/weak references and cycles. | Pick the right callback and ownership relationship; review cleanup and retained references explicitly. |
| Build scenarios and persistence | [General Game Mode Setup](https://community.bistudio.com/wiki/Arma_Reforger:General_Game_Mode_Setup) connects game modes, managers, spawning and respawn components. [Persistence System](https://community.bistudio.com/wiki/Arma_Reforger:Persistence_System) describes tracking, serialization, storage scope and asynchronous restoration. | Trace the full dependency chain and load order, rather than implementing one isolated class. |
| Keep mods performant | [Scripting: Performance](https://community.bistudio.com/wiki/Arma_Reforger:Scripting:_Performance) explains eliminating repeated work, caching, reduced frequency and spreading work over frames. [Model Performance](https://community.bistudio.com/wiki/Arma_Reforger:Model_Performance) addresses geometry and LOD costs. | Use these as review questions and measurement targets. Model Performance labels itself a work in progress and ties examples to version 1.6.0; its examples are not universal budgets. |
| Prepare and publish assets | The captured weapon, vehicle, character, Blender, material, audio and animation tutorials connect exported assets to engine configuration. [Mod Publishing Process](https://community.bistudio.com/wiki/Arma_Reforger:Mod_Publishing_Process) describes preparation and Workshop workflow. | Keep original project sources and test the authored result before publication. This research did not publish or modify any mod. |

These are documented workflows, not proof that every example is compatible with the currently installed game. For an actual mod change, check named APIs against installed script declarations and validate the result in Workbench.

## Improvements supported now

1. **Answer “what and why” with the actual page context.** Research returns a bounded selection with applicable warnings. Follow its section-reading call for surrounding material, heading ancestry and table labels.
2. **Retrieve examples without corrupting formatting.** Code search recognizes the wiki's custom Enforce highlighter as well as ordinary `pre` blocks. Long examples can be reconstructed through character-paged `wiki_read` calls.
3. **Browse the official taxonomy.** Category and title searches expose scripting, asset, audio, terrain, scenario, Game Master and editor-tool documentation. Exact category filtering means direct membership; child categories should be browsed explicitly.
4. **Keep claims auditable offline.** Every result identifies its captured page revision and acquisition method. Rendered HTML is never described as original wikitext, and capture time is never described as a live freshness check.
5. **Refresh deliberately.** A new browser capture can replace the index explicitly. Import reports added, changed, unchanged and absent-from-new-capture pages. Reimporting unchanged files is local validation, not an online update check.

## Prototype integration and remaining opportunities

`research_mod` now connects a bounded selection of wiki file links to installed PAK paths, returns short ranked guidance and provides exact follow-up calls. It preserves original links and historical line hints without invoking editor actions. This implements the first connection below at file level; it does not resolve individual symbols, verify APIs or automatically assemble every prerequisite and warning.

- **Connect wiki examples to installed declarations and resources.** The 801 `enfusion://` links supply useful file targets. The prototype parses supported script/resource links and matches paths to the prepared catalogue. Symbol-level verification remains a follow-up source-reading task.
- **Read contextual evidence.** The existing `wiki_read` now supports bounded sections and associated global/ancestor warnings. Tables preserve merged-cell/header relationships and meaningful image alternatives. This improves context without claiming to assemble every prerequisite or runtime validation step automatically.
- **Curate official sample projects.** The pages link to Bohemia's `Arma-Reforger-Samples` and `Arma-Reforger-Misc` repositories, including vehicle, weapon, prop, character and faction examples. The links are preserved but those repositories have not been captured or verified in this task. One captured GitHub URL contains an apparent `|separate` suffix, demonstrating why discovered links should not automatically be treated as validated downloads.
- **Add targeted update discovery later.** Revision comparison can avoid unnecessary recapture once a permitted, reliable online revision-check route is established. Current refresh is an explicit complete browser pass; there is no scheduled or automatic check.

## Validation and limits

Real stdio MCP checks exercise imported content with `RplRpc` code search, `replication` prose search, scripting category search, prefab title search and category-filtered class examples. Code extraction was compared against every saved DOM code block on Create a Component, Enforce Script Syntax, Multiplayer Scripting and Scripting: Performance.

Validation uncovered and fixed two acquisition/extraction issues: the browser tool shortened very large strings, so eight pages were recaptured in verified chunks; and the wiki's custom Enforce highlighter required explicit code recognition. Duplicate rendering included “Copy” UI text, which is now excluded from indexed code. Earlier incomplete files and failing test logs are preserved. An initial test incorrectly expected `RplComponent` in a code block; the corpus contains it in prose, while `RplRpc` is present in actual examples.

Evidence is kept locally under `.temp/artifacts/wiki-browser-capture/` (browser originals and manifest), `.temp/artifacts/wiki-data/wiki/` (versioned local indexes and full HTML), and `.temp/artifacts/wiki-validation/` (imports, link audit, statistics and test outputs). These local artifacts are not bundled into distributable server packages.

Coverage excludes unreachable/uncategorized pages, media binaries, external documentation and revision history. We have not executed the documentation's examples in the game, validated all linked downloads, or produced a fully self-contained visual website mirror. The useful deliverable is a persistent, provenance-aware local documentation corpus and working offline MCP retrieval.
