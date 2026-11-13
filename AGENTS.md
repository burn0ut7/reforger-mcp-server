# Reforger MCP

Build a standalone C#/.NET server using the official MCP SDK. Share one provider-independent implementation and tool contract across clients; keep transport, releases and plugin packaging thin.

Use the [Reforger skill](skills/reforger/SKILL.md) for Reforger work, including MCP tooling. Pass relevant instructions to subagents. The skill owns research, authoring and proportionate testing workflows.

## Project boundaries

- Reforger MCP, Lobby2, and the GC website are three separate projects. Keep their source ownership, builds, packages, installation, configuration, and runtime dependencies independent.
- This repository owns only the generic Reforger MCP server and its provider-independent tooling. Lobby2 features, GC Tools, GC-specific Workbench extensions, installers, and external dependencies belong to Lobby2, not this server. Website features belong to the website repository.
- GC Tools and everything installed through it are Lobby2 products. They must not bundle, install, launch, reference, or compile source from Reforger MCP. Do not use the MCP executable as a GC installation/bootstrap helper or make MCP installation a prerequisite for Lobby2.
- Reforger MCP must not depend on Lobby2 or the website. Using the generic MCP tools to develop or verify an external project does not make that project's implementation part of MCP.
- Do not link source files across these repositories, publish one project's executable into another project's package, or treat similar branding/UI as shared application ownership. Exchange only explicitly defined integration data where a feature requires it.
- Existing cross-project code and packages are migration debt, not precedent or permission to extend coupling. Preserve unfinished work when relocating it; do not claim separation is complete until independent builds and package contents are verified. See [current boundary violations](docs/project-boundaries.md).

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
