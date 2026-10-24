---
name: reforger
description: Use for any Arma Reforger or Reforger-modding question or task, including research, Workbench, gameplay, scripts, UI, assets, and related MCP tooling. Apply official wiki guidance, installed game sources, and Reforger MCP tools where relevant.
---

# Reforger

Use the relevant guidance below to answer Reforger questions or carry out requested work; apply authoring and testing steps only when the task calls for them. For mod work, establish the addon/project, relevant dependencies, and an observable result; reuse context already established.

Follow applicable global and project AGENTS.md instructions. The main agent coordinates collaboration and independent review under that guidance; delegated readers and workers stay within their assigned roles.

## Find the right approach

Use the saved official wiki as the primary workflow reference. Start unfamiliar questions with `research_mod`, then follow its `wiki_read` calls for section context, prerequisites, warnings, and examples. For a particular guide, use `wiki_search` with its title. Preserve revision provenance and “Do/Don't” labels; captured documentation is not a live compatibility check. Missing coverage is a gap, not proof that a feature does not exist.

Use the [curated wiki entry points and workflow map](references/workflows.md) for unfamiliar language/engine basics or to follow a feature across systems. It is a starting point: continue searching beyond it for the task's details. Read only relevant sections; reuse evidence while its source revision remains applicable. An existing scenario framework, prefab variant, or configuration change may already fit the request. Use custom Enforce Script where it adds the needed behavior.

Check relevant APIs and usage in installed sources with `pak_search_text` and `pak_read`; use `pak_search` for resources. Follow the current tool schemas and reported index state. Keep authored files in the addon, separate from reference mirrors and installed archives. Extracted `.c` files are script source; generated native declarations expose signatures, not native implementations. Reserve `engine_*` investigation for unresolved native behavior; decompiled function names do not establish callable script APIs.

## Build and verify

When the task creates, changes, or diagnoses player-facing UI, read and follow the [UI workflow](references/ui-workflow.md) before design or edits. This includes layouts, HUDs, menus, dialogs, labels, input/focus, and script-driven visibility or feedback, even without a `.layout` change. Skip it for work that does not affect UI; apply only relevant checks to narrow UI fixes. New UI or substantial redesign requires a saved visual concept before implementation and comparison with actual MCP screenshots afterward.

Before writing or revising Enforce Script, read [coding conventions and readability checks](references/scripting-style.md). Apply them to new code and touched logic, including snippets and generated scripts. Use the addon's existing creator tag; preserve engine overrides and externally referenced names. Check a relevant official or installed example for API usage, without copying its style defects or unrelated scaffolding.

Include the authoring work that makes code usable: appropriate script module, editor-visible declarations, component/action attachment, resource references, and scenario setup as applicable. Respect the intended instance, prefab, or game-wide scope.

Read [Workbench guidance](references/workbench-workflow.md) before editor mutations or live tests. Prefer the semantic MCP operation for the task. If the server is disconnected or the operation unsupported, report that boundary; do not invent tools or substitute another execution backend.

Verify the requested behavior and saved result, not just compilation. For scenarios, follow the player's path from joining and spawning through objectives and completion; for multiplayer, check the relevant authority and peers. Report what was tested and what remains unverified. Publishing is a separate user-directed action.

Before delivery, run the coding reference's readability check against the actual saved scripts. Treat formatting review and runtime verification as separate checks; neither proves the other. State any intentional compatibility exceptions.

## Optional gap logging

`debug=true` in the request enables tooling-gap notes; it is a skill preference, not an MCP argument. Default is off. In either mode, diagnose ordinary fixable errors, but stop the dependent operation when evidence establishes an unsupported capability or uncertain mutation. Inspect before any replay; continue independent work where useful.

With debugging enabled, append each distinct gap once to the project's existing development log, or `docs/reforger-tooling-gaps.md` outside runtime import folders. Create no log without a gap; if project writes are unavailable, return the note in chat. Use two sentences: the intended action and observed obstacle, then the capability or evidence needed to resolve it. Include a concise tool/state or evidence locator when available; avoid speculative diagnoses.

Example: “I need to inspect a dedicated-server component, but `workbench_runtime_inspect` supports only embedded Play. I would like supported standalone-runtime inspection to verify its server-side state.”

Do not turn these notes into automatic MCP-server changes or log every routine script error as a tooling gap.
