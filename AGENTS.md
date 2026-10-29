# Reforger MCP

Build a standalone C#/.NET server using the official MCP SDK. Share one provider-independent implementation and tool contract across clients; keep transport, releases and plugin packaging thin.

Use the [Reforger skill](skills/reforger/SKILL.md) for Reforger work, including MCP tooling. Pass relevant instructions to subagents. The skill owns research, authoring and proportionate testing workflows.

## Design

- One authoritative implementation per capability; no fallback backends. Fix the owner and report unsupported, unavailable, stale or incompatible states explicitly.
- Prefer small public interfaces hiding cohesive implementation. Keep reusable capabilities addon-agnostic; project scenarios/helpers belong in separate test addons.
- Server logic: C#/.NET. Native analysis: Ghidra with minimal Java. Enforce: live engine access. Preserve native reading and its dependencies; Workbench reconstruction, Rust translation and compiler/packer orchestration remain discontinued.
- Ship direct owning-function hooks where feasible. UI traversal supports discovery/testing; document a concrete limitation when shipping it. It is not recovery for a failed direct hook.
- Tool metadata describes capabilities, inputs, outputs and consequential side effects. Implement preconditions; keep workflow guidance in skills.
- Preserve source/version provenance and installed archives. Distinguish source, declarations, converted resources, inferred native code and live observations.

## Modding

Use Reforger MCP for editor/game interaction and verification; ordinary editing/builds use development tools. Repair missing test capabilities in their owning MCP implementation.

Keep the current Workbench project and test addons loaded; iterate with Reload. Preserve settings and authored work; explain necessary lifecycle changes.

Exercise production handlers and authority checks. Verify outcomes rather than dispatch; inspect uncertain mutations before replay.

Shared `modded class` overrides apply globally while loaded. Gate project behavior on mode ownership, preserve inherited behavior elsewhere and release state on exit. Validate isolation across mode changes against an addon-free baseline.

Follow the skill's Enforce conventions; preserve serialized, protocol, reflection and callback contracts unless deliberately updating their consumers.
