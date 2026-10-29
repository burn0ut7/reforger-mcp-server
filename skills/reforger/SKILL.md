---
name: reforger
description: Use for any Arma Reforger or Reforger-modding question or task, including research, Workbench, gameplay, scripts, UI, assets, and related MCP tooling. Use installed API declarations, source examples, resource data, and Reforger MCP tools.
---

# Reforger

Use the relevant guidance below to answer Reforger questions or carry out requested work; apply authoring and testing steps only when the task calls for them. For mod work, establish the addon/project, relevant dependencies, and an observable result; reuse context already established.

Follow applicable global and project AGENTS.md instructions. The main agent coordinates collaboration and independent review under that guidance; delegated readers and workers stay within their assigned roles.

## Find the right approach

Start with installed code and resource data. Use the [source research workflow](references/workflows.md) to trace API declarations, base classes, implementations, and real call sites, then inspect the prefabs/configuration that connect them. Prefer an existing framework or resource variant where it fits the requested behavior.

Prefer supported built-in engine APIs, existing game systems/components, and resource configuration over reimplementing their behavior in script. Before adding a custom helper or subsystem, look for the operation already owned by the engine or an existing framework and inspect how shipped code uses it. Keep custom code focused on project-specific policy and composition; explain the concrete capability gap when a replacement is necessary. Native implementation is a reason to investigate reuse, not proof of performance or reliability; verify suitability and measure consequential performance claims.

Research unfamiliar or consequential APIs thoroughly before design or coding. Follow declarations into base behavior, representative callers, setup/teardown, and associated resources; deepen the investigation for unclear lifecycle, authority, ownership, side effects, or performance. Use the research workflow's depth criteria, preserving compact evidence rather than stopping at a matching signature or indiscriminately loading files.

Check `pak_status` and prepare explicitly if needed. Use bounded `pak_search_text` batches for related symbols, `pak_search` for resource paths, and `pak_read` for surrounding code and readable prefab/config/layout data. Follow the tools' current schemas and revision tokens; narrow by source or path and reuse inspected evidence instead of loading whole files repeatedly. Empty matches are not proof that a feature does not exist.

Keep authored files separate from reference mirrors and installed archives. Extracted `.c` files are script source; generated native declarations expose signatures, not native implementations. Reserve `engine_*` investigation for unresolved native behavior; decompiled function names do not establish callable script APIs. Disk data does not prove effective override precedence or live behavior.

## Build and verify

Every code change must pass a fresh compile gate before completion. Every changed UI element must also pass live interaction, current MCP screenshots and an independent visual critique; follow the verification gates in the linked workflows.

Keep the current Workbench process and development project open. Use the single MCP Reload call to recompile and activate script changes with World Editor left open. The tool stops embedded Play when needed, verifies it stopped, then reloads; no separate caller-side Stop or editor close/reopen is needed. Test addons and fixtures may stay loaded between iterations, tasks, and handoff; keep them separate from production code without routinely switching projects or unloading them. Gray has authorized autonomous control of Workbench and its prompts for requested work, including script authorization, access prompts, closing, reopening, restarting and necessary project changes. Do not ask again for these Workbench actions; inspect the prompt and choose the response that advances the task. Prefer Reload and preserve authored work and settings; use lifecycle changes when needed. This standing authorization is specific to Workbench, not blanket control of unrelated applications or external services.

When the task creates, changes, or diagnoses player-facing UI, read and follow the [UI workflow](references/ui-workflow.md) before design or edits. This includes layouts, HUDs, menus, dialogs, labels, input/focus, and script-driven visibility or feedback, even without a `.layout` change. Skip it for work that does not affect UI; apply only relevant checks to narrow UI fixes. Default to monochrome, yellow interaction states, translucent surfaces and concise wording using existing game UI resources. New UI or substantial redesign requires a saved visual concept before implementation and comparison with actual MCP screenshots afterward.

Before writing or revising Enforce Script, read [coding conventions and readability checks](references/scripting-style.md). Apply them to new code and touched logic, including snippets and generated scripts. Use the addon's existing creator tag; preserve engine overrides and externally referenced names. Check a relevant official or installed example for API usage, without copying its style defects or unrelated scaffolding.

Include the authoring work that makes code usable: appropriate script module, editor-visible declarations, component/action attachment, resource references, and scenario setup as applicable. Respect the intended instance, prefab, or game-wide scope.

Read [Workbench guidance](references/workbench-workflow.md) before editor mutations or live tests. Prefer the semantic MCP operation for the task. If the server is disconnected or the operation unsupported, report that boundary; do not invent tools or substitute another execution backend.

For immediate live component method execution, use `workbench_runtime_call`; it performs target resolution, signature validation and one invocation internally. Use separate prepare/invoke only for intentional validation-only or two-step work. The receipt and synchronous result still need relevant state/log verification. Keep compact success responses; request `workbench_operation(operationId, details=true)` only when original adapter diagnostics are needed. Never rerun a method to recover details; operation history is bounded and is not fresh observation.

Verify the requested behavior and saved result, not just compilation. For scenarios, follow the player's path from joining and spawning through objectives and completion; for multiplayer, check the relevant authority and peers. Report what was tested and what remains unverified. Publishing is a separate user-directed action.

Before delivery, run the coding reference's readability check against the actual saved scripts. Treat formatting review and runtime verification as separate checks; neither proves the other. State any intentional compatibility exceptions.

## Optional gap logging

`debug=true` in the request enables tooling-gap notes; it is a skill preference, not an MCP argument. Default is off. In either mode, diagnose ordinary fixable errors, but stop the dependent operation when evidence establishes an unsupported capability or uncertain mutation. Inspect before any replay; continue independent work where useful.

With debugging enabled, append each distinct gap once to the project's existing development log, or `docs/reforger-tooling-gaps.md` outside runtime import folders. Create no log without a gap; if project writes are unavailable, return the note in chat. Use two sentences: the intended action and observed obstacle, then the capability or evidence needed to resolve it. Include a concise tool/state or evidence locator when available; avoid speculative diagnoses.

Example: “I need to inspect a dedicated-server component, but `workbench_runtime_inspect` supports only embedded Play. I would like supported standalone-runtime inspection to verify its server-side state.”

Do not turn these notes into automatic MCP-server changes or log every routine script error as a tooling gap.
