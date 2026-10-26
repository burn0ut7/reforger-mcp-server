# Codex instructions

Use judgment and infer intent. Keep solutions and documentation concise; avoid unnecessary abstractions and clarification loops.

For every Arma Reforger or Reforger-modding question or task, including discussion and related MCP-server work, use the [Reforger skill](skills/reforger/SKILL.md). Include the skill and applicable global/project instructions in related subagent handoffs.

## Project

Build a standalone, downloadable Reforger MCP server in C#/.NET using the official MCP SDK. Expose Workbench operations, supported game-runtime commands, Reforger file and language inspection, and installed API/resource research through standard MCP.

Support ChatGPT/Codex, Claude, and other compatible clients through the same server and tool contracts. GitHub releases and client-specific plugin packages are distribution wrappers, not separate implementations. Keep the core independent of model providers.

## MCP-driven modding and testing

- Use the Reforger MCP server for Arma Reforger and Workbench interactions, including project/world operations, runtime inspection, navigation, button actions, screenshots, and validation. Do not use Computer Use, desktop input injection, or an alternate automation backend for these interactions. Ordinary source editing and local build/file operations may use development tools.
- Make required test behaviors scriptable through MCP. If a capability is missing or broken, implement or repair it in the owning MCP capability before relying on it; report unsupported states explicitly rather than switching control paths.
- Keep reusable editor/runtime capabilities addon-agnostic in the MCP server. Keep project-specific scenarios, navigation helpers, assertions, and fixtures in an optional test addon owned by that project, separate from production code. Test addons may remain loaded throughout the development session. Invoke those helpers through the generic MCP runtime interface; do not hard-code project behavior into the server.
- Keep the current Workbench process and development project open. Use Reload to recompile and activate script changes; do not routinely quit/relaunch Workbench or switch between production and test projects. Leave test addons loaded between tasks and at handoff. Only change the project or restart when the user requests it or a specific operation demonstrably requires it; explain that requirement first. A tool restriction is not evidence that Workbench itself requires a restart: report or repair the owning MCP limitation rather than adding a restart cycle.
- Exercise production handlers and authority checks rather than duplicating gameplay logic or bypassing permissions. Verify resulting state, receipt-correlated logs, and MCP screenshots where relevant; dispatch success alone is not a passing test. Preserve uncertain outcomes and never blindly replay mutations.

## Architecture

- For Enforce Script adapters and generated `.c` code, follow [Reforger scripting conventions](skills/reforger/references/scripting-style.md), including its pre-delivery readability check. Keep C# and Java under their own language conventions. Preserve protocol, reflection, serialized-field and engine callback names unless a coordinated contract change is intended.

- The Rust Workbench reconstruction effort is discontinued. Preserve the native-code reading capability and its required analysis dependencies; do not resume editor reconstruction, automatic Rust translation, or compiler/packer orchestration.
- One uniform system, one authoritative implementation per capability. No fallbacks, secondary systems, cascading alternatives, duplicate backends, or silent substitutions.
- Fix failures in the owning implementation. Never route around a broken capability by adding another system. Report unsupported, unavailable, stale, or incompatible states explicitly; never disguise them as success.
- Use deep modules: small, clear public interfaces that hide implementation complexity, with explicit ownership and dependencies. Avoid needless layers and frameworks.
- Keep server logic in C#/.NET. Use Ghidra as the sole native-engine decompiler, with a minimal Java adapter; use Enforce Script only where live engine access requires it. PAK inspection, searching, and read-only extraction for modding are in scope; leave installed archives unchanged. Distinguish archive decompression, existing script source, resource conversion, and native-code decompilation.
- Prefer direct hooks into the owning Workbench functions for shipped operations. Menus, buttons, and other UI traversal are for research, discovery, testing, and prototyping; the final implementation must call the owning function directly whenever feasible and appropriate. Native invocation of a menu/button is still a UI action, not a substitute for a semantic function hook. Document a concrete limitation before shipping a capability that requires UI traversal; never add it as a fallback for a failed direct call. Preserve the user's current settings by default, while supporting deliberate, explained configuration changes.
- Keep modding capabilities addon-agnostic. Test addons and fixture names belong in tests and evidence, not production behavior. Add restrictions, layers, or special cases only when they provide clear correctness, safety, or usability value.
- When authoring mods, treat shared `modded class` overrides (especially VON/voice) as global while the addon is loaded. Gate project-specific behavior on the active game mode's ownership, preserve inherited behavior when inactive, and release project-owned shared state on exit. Verify unrelated-mode behavior through MCP with the addon loaded and after mode changes against an addon-free baseline; compilation and source guards alone are insufficient.
- Keep MCP transport and client packaging thin. Any supported connection modes must be explicitly configured and share the same implementation; never cascade between them.
- Preserve source and version provenance for indexed files and language data. Do not present cached data as live state or declarations as recovered compiled code.
- Verify real end-to-end behavior. A failure requires a root-cause fix, not a workaround path; never blindly replay a game or editor mutation after an uncertain outcome.
