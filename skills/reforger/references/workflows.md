# Research APIs and resources from installed sources

Use this workflow on demand. Start from the requested behavior and one likely class, method, or resource name; avoid loading broad inventories when a focused search will answer the question.

## Establish the source

Check `pak_status` for coverage and freshness. If unprepared, explicitly prepare the configured installation or the observed project/addon context with `pak_prepare`. Use source labels or addon GUIDs returned by the tools. Preserve revision tokens while paging; report unavailable, stale, unreadable, or missing coverage rather than inferring absence. Installed archive and loose copies can coexist; a matching path does not establish which override is active.

## Follow an API into working code

1. Use `pak_search_text(query="ClassOrMethod", limit=5)` to locate declarations, usages and resource examples across all indexed text. For related symbols, use `queries` with 1–8 short searches in one scan; batch mode returns up to three matches per query. Terms within a query must occur on the same line, so search identifiers rather than natural-language questions.
2. Read each relevant match with `pak_read(entryId=..., offset=line-1, limit=40, revision=...)`. Start a few lines earlier when necessary to include the method signature or owning class. Follow pagination only when the missing context matters.
3. Trace the base class and overridden method, argument types, defaults, return values, and actual implementation. Separate a native declaration from script implementation; a signature alone does not explain side effects or lifecycle requirements.
4. Read a representative caller and its setup/teardown. Check who owns the object, how it obtains dependencies, when callbacks run, event masks, authority/replication checks, and cleanup. Find a second caller when the first relies on unusual assumptions.
5. Narrow subsequent searches by `source` or `pathQuery`. Keep exact entry IDs, line locations, and revisions with findings; reuse evidence while its revision remains applicable. State what is observed versus inferred.

Use `engine_*` only for unresolved native behavior that source cannot explain. Native analysis remains inferred implementation evidence, not proof of a callable script API. Confirm authored behavior through supported MCP compilation and runtime tests.

Run native-analysis queries sequentially. Poll preparation job IDs with `engine_job`; only `ready` establishes completed preparation. Inspect warnings and context before interpreting inferred code. Combine code pages only when their `codeSha256` values match.

## Research depth and reuse decisions

Before designing custom behavior, search for the capability in supported engine APIs and existing game systems, then examine how shipped scripts and resources integrate it. Search related terminology and likely owning/base classes when the first identifier search fails. Prefer the public operation that owns the behavior, including its lifecycle and authority checks, over reproducing internal logic or manipulating its state directly. Existing script frameworks and prefab/configuration mechanisms also count as reuse; native code is not the only useful built-in functionality.

Deepen research when the API is unfamiliar, examples disagree, behavior crosses systems, or correctness depends on replication, object lifetime, persistence, or frequent execution. Follow the relevant base/override chain, multiple representative callers, initialization and cleanup, error/return handling, and resource wiring until the consequential assumptions are supported. Inspect contrasting use cases when context could change the contract. Stop when the requested behavior and integration constraints are understood; record unresolved gaps and verify them through targeted MCP tests rather than accumulating unrelated source.

Use custom code for project-specific rules, orchestration, or a demonstrated missing capability. Before replacing existing functionality, state what was inspected and why its contract cannot serve the requirement. Avoid wrappers that merely rename an existing operation or parallel systems that duplicate its ownership. Do not bypass permissions or supported interfaces to reach a native implementation.

Treat performance as a workload question. Prefer an appropriate engine operation over duplicating its work in script, but consider call frequency, allocation, engine/script boundary costs, and the amount of work requested. Measure important performance claims under representative conditions; native implementation alone does not guarantee the fastest or most reliable outcome. Keep generated native declarations, actual script implementations, inferred native behavior, and runtime observations distinct.

## Understand a prefab or configuration

Find the named resource with `pak_search(query="ResourceName", kind="prefab", limit=5)` (or the appropriate config/layout kind). Read the exact entry with `pak_read`; it supports readable prefab/config/layout text as well as scripts. Search contents directly with `pak_search_text(query="LadderComponent", pathQuery=".et")` for prefab examples, or a widget class/property with `pathQuery=".layout"` for UI examples. Omit pathQuery to search all supported text formats. Attribute/value queries match terms on one line; component and attribute declarations on separate lines need separate searches and contextual reads. Check exclusions and `pak_status` coverage before claiming absence.

Content hits identify explicit text, not every inheriting child. Compare relevant examples across sources rather than taking the first hit. Trace the parent resource and inherited variants, then inspect local overrides, entity/component classes, serialized attributes, child entities, and linked resources. Resolve each reference using the returned path/identity and source context; do not invent GUIDs or infer override precedence from duplicate paths. Follow component classes into their declarations and consuming code to explain what each relevant value does. Inspect linked configuration, layouts, materials, or scenario setup only as needed for the behavior under investigation.

Distinguish a stored default, an inherited value, an instance override, and a value changed at runtime. Summarize the prefab's purpose through its components and connections, identifying any uninspected parents or references. For binary or unsupported encodings, report the boundary: `pak_extract` preserves bytes and provenance but does not convert resource formats. Use supported Workbench MCP inspection where applicable to establish resolved or live state.

## Finish with evidence

Give the smallest explanation supported by the inspected code and resources: API contract, representative usage, required setup, relevant lifecycle/authority limits, and remaining uncertainty. For changes, compile and exercise the actual behavior through MCP; search matches and successful dispatch alone are not verification. Follow the [UI workflow](ui-workflow.md) for player-facing UI and [Workbench workflow](workbench-workflow.md) before editor mutations or live tests.
