# Codex instructions

Use judgment and infer intent. Keep solutions and documentation concise; avoid unnecessary abstractions and clarification loops.

## Project

Build a standalone, downloadable Reforger MCP server in C#/.NET using the official MCP SDK. Expose Workbench operations, supported game-runtime commands, Reforger file and language inspection, and indexed official-wiki search through standard MCP.

Support ChatGPT/Codex, Claude, and other compatible clients through the same server and tool contracts. GitHub releases and client-specific plugin packages are distribution wrappers, not separate implementations. Keep the core independent of model providers.

## Architecture

- For Enforce Script adapters and generated `.c` code, follow [Reforger scripting conventions](skills/reforger/references/scripting-style.md), including its pre-delivery readability check. Keep C# and Java under their own language conventions. Preserve protocol, reflection, serialized-field and engine callback names unless a coordinated contract change is intended.

- The Rust Workbench reconstruction effort is discontinued. Preserve the native-code reading capability and its required analysis dependencies; do not resume editor reconstruction, automatic Rust translation, or compiler/packer orchestration.
- One uniform system, one authoritative implementation per capability. No fallbacks, secondary systems, cascading alternatives, duplicate backends, or silent substitutions.
- Fix failures in the owning implementation. Never route around a broken capability by adding another system. Report unsupported, unavailable, stale, or incompatible states explicitly; never disguise them as success.
- Use deep modules: small, clear public interfaces that hide implementation complexity, with explicit ownership and dependencies. Avoid needless layers and frameworks.
- Keep server logic in C#/.NET. Use Ghidra as the sole native-engine decompiler, with a minimal Java adapter; use Enforce Script only where live engine access requires it. PAK inspection, searching, and read-only extraction for modding are in scope; leave installed archives unchanged. Distinguish archive decompression, existing script source, resource conversion, and native-code decompilation.
- Prefer direct hooks into the owning Workbench functions for shipped operations. Menus, buttons, and other UI traversal are for research, discovery, testing, and prototyping; the final implementation must call the owning function directly whenever feasible and appropriate. Native invocation of a menu/button is still a UI action, not a substitute for a semantic function hook. Document a concrete limitation before shipping a capability that requires UI traversal; never add it as a fallback for a failed direct call. Preserve the user's current settings by default, while supporting deliberate, explained configuration changes.
- Keep modding capabilities addon-agnostic. Test addons and fixture names belong in tests and evidence, not production behavior. Add restrictions, layers, or special cases only when they provide clear correctness, safety, or usability value.
- Keep MCP transport and client packaging thin. Any supported connection modes must be explicitly configured and share the same implementation; never cascade between them.
- Preserve source and version provenance for indexed files, language data, and wiki results. Do not present cached data as live state or declarations as recovered compiled code.
- Verify real end-to-end behavior. A failure requires a root-cause fix, not a workaround path; never blindly replay a game or editor mutation after an uncertain outcome.
