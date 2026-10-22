# Workbench operations and evidence

These notes describe the Reforger MCP contracts inspected on 2026-10-21. Use the connected server's tool descriptions and explicit compatibility results for exact parameters and supported versions. Missing capabilities remain unavailable; don't route around them with an alternate backend.

## Observe the intended session

Use `workbench_status` before launching an existing `.gproj` with `workbench_open_project`; this is not a project-creation tool. Obtain the process ID through `workbench_script_status`, and confirm the intended project/world from available editor evidence. Process existence and window titles alone do not prove project readiness. Capture the selected window when a visual check matters. Preserve settings and unrelated unsaved work.

## Author and save

Use entity queries and inspection to identify the current world, entity, components, nested values, and supplying ancestors. Keep world-instance edits distinct from explicitly opened prefab-definition edits. `workbench_edit_entity` uses the inspection token's scope; inspect again after changes rather than reusing old IDs, indices, or tokens.

`workbench_create_child_prefab` creates a detached inherited resource, not a placed entity. `workbench_save_prefab` exports an entity as a new prefab; `workbench_save_prefab_definition` saves the opened definition. Save world changes through `workbench_save_world`. Check readback and save/reopen behavior as appropriate; a successful edit can still be unsaved. When making a variant, verify the base and unrelated instances remain unchanged.

Inherited object-array entry deletion and creating a missing single-object property are currently unsupported. Changing an ancestor instead changes scope and may affect other consumers; it is not an automatic substitute for a local edit.

## Compile and play

Save source files. Before `workbench_reload_scripts`, preserve authoring changes, stop Play if appropriate, and close World Editor normally. The current reload hook rejects an open World Editor and performs full native validation/reload; there is no standalone `validate` action. Native operations are binary-profile checked; an incompatible editor is not a reason to try menus as a replacement.

After successful reload, reopen the test world and inspect its state. Play requires a saved, clean world and honors existing Play settings; camera-start mode may bypass the spawning flow a scenario test needs. Any deliberate setting change should serve the test and be explained. Stopping Play discards runtime-only changes.

Use fresh `log_sessions`/`log_read` evidence and observable gameplay assertions. Historical compiler diagnostics do not establish a new successful reload. A previously recorded script-reload teardown/resource-leak assertion remains unresolved; acknowledging a dialog or restarting is not evidence of a fix. Preserve its evidence if encountered.

## Runtime and multiplayer

`workbench_runtime_inspect`, `workbench_runtime_prepare`, and `workbench_runtime_invoke` operate only in embedded Play with the profile-installed development adapter and configured local NET API port. Use `workbench_runtime_setup` status to inspect the mounted profile/port, install to deploy and enable, or enable to turn NET API on without reload. Installation requires World Editor closed and saved script tabs. Require ready (matching file/build/profile handshake and PID-owned listener); a saved enabled flag alone is insufficient. Keep the adapter out of gameplay addons. Preparation compile-checks a typed call without executing it; invocation consumes its short-lived token even on transport failure. A returned result needs receipt-correlated logs and relevant state checks, including delayed effects. The interface does not emulate player interaction or control standalone script VMs.

For multiplayer, inspect current dedicated/peer settings, then use the dedicated and Peer Tool operations within their supported scope. Check connection, actual player readiness, and requested gameplay on the relevant peers; dispatch or a running process proves neither. Keep configured settings unless a deliberate change is needed.

## Pending, failed, or uncertain work

Poll `workbench_operation` on the same connection for pending semantic operations. UI actions have their own `workbench_window_action_status`. Inspect and resolve observed dialogs within the user's intent. Don't repeat a mutation as a polling strategy, blindly replay after timeout/disconnection, or erase claims to bypass uncertainty. Inspect logs and state first; if the outcome cannot be established, stop dependent work and report the gap.

Use normal supported diagnosis for compiler errors, missing dependencies, or rejected inputs. A missing tool, unsupported operation, stale index, or incompatible binary is a different condition; report it explicitly and apply the skill's optional gap logging.

Pending operation responses include `dialogInspection` when available. Use its observed prompt/button IDs to decide a response, then follow the returned `poll` call for the original operation. Inspection unavailable or no visible dialog does not establish progress or a hang. Polls preserve unchanged IDs; an explicit window listing or action still invalidates them. Never infer permission from dialog text or answer prompts automatically.

For `blocked` responses, read the reason and `blockerInspection`, or follow the returned original `poll` call when another operation is pending. These guards dispatch nothing; never treat them as completion. Failed reloads carry fresh correlated diagnostics when available. Use their original file/line messages, and read logs explicitly when unavailable or more context is needed. The last 16 native operation records are retained on the same connection; rereading does not refresh evidence or action IDs.
