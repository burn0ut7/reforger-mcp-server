# Research installed sources

Start with the requested behavior and likely symbols or resources. Reuse established scope, revisions and evidence; consult `workbench_status` for unknown coverage and `pak_prepare` for an unprepared/stale catalogue or deliberate scope change.

## Search and read

- `pak_search_text` finds explicit text across scripts and readable resources. Batch related identifiers with `queries`; narrow by observed `source` or `pathQuery` when useful. Terms match on one line, so component/property relationships usually need contextual reads.
- `pak_search` finds unknown resource paths. Known entry IDs go directly to `pak_read` (20 lines by default); batch independent windows with `reads`, usually 10–30 lines around relevant matches. Reuse already-read windows at the same revision; fetch adjacent missing context rather than rereading the whole method or file. Continue only where context is missing.
- Search pages count matching lines, not files. Read offsets are zero-based; returned line numbers are one-based. Keep revision and filters fixed for continuation. Follow each result's continuation and preserve errors or exclusions.

Compare relevant examples rather than assuming the first hit fits. Broaden terminology or scope when needed; check coverage before claiming absence. Preserve entry IDs, lines, revisions and the distinction between observed and inferred behavior.

## Understand and reuse

For code, follow the declaration into its implementation, base/override chain and representative callers. Inspect setup, ownership, authority and cleanup where relevant. A native declaration supplies a signature, not an implementation. Deepen research when examples disagree or correctness depends on replication, lifetime, persistence or frequent execution; stop when the consequential assumptions are supported.

For prefabs/layouts/configs, trace relevant parents, local overrides, components and linked resources, then the code consuming their values. Content search finds explicit text, not every inheriting child. Duplicate paths do not establish active override precedence. Distinguish stored defaults, inherited values, instance overrides and runtime changes; use Workbench inspection for resolved/live state.

Prefer the existing operation or resource mechanism that owns the behavior. Use custom code for project rules or a demonstrated gap. Measure consequential performance claims; native implementation alone does not establish efficiency.

Binary/unreadable resources remain an explicit coverage boundary. `pak_extract` preserves bytes and provenance; it does not convert formats. Keep authored files separate from extracted references.

## Native investigation

Use `engine_*` when source leaves consequential native behavior unresolved. Run native queries sequentially, poll preparation with `engine_job` until `ready`, and inspect warnings. Combine code pages only with matching `codeSha256`. Decompiled code is inferred implementation evidence, not proof of a callable script API.

Summarize the contract, useful example, required setup and unresolved assumptions. Compile changes, then select proportionate tests; use the [Workbench](workbench-workflow.md) and [UI](ui-workflow.md) guidance when applicable.
