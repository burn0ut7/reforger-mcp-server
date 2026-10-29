# Workbench operations and evidence

These notes describe the Reforger MCP contracts inspected on 2026-10-21. Use the connected server's tool descriptions and explicit compatibility results for exact parameters and supported versions. Missing capabilities remain unavailable; don't route around them with an alternate backend.

## Observe the intended session

Reuse the known process/project identity within the established session; refresh live observations when a state-dependent decision needs them. Status calls are not a preamble to every operation: semantic operations validate their own prerequisites, and their results include state and polling information. Poll the returned operation rather than redispatching it.

Use `workbench_status` before launching an existing `.gproj` with `workbench_open_project`; this is not a project-creation tool. Obtain the process ID through `workbench_script_status`, and confirm the intended project/world from available editor evidence. Process existence and window titles alone do not prove project readiness. Capture the selected window when a visual check matters. Preserve settings and unrelated unsaved work.

Use semantic tools for supported operations. For native UI inspection, list windows once, descend through returned control IDs, and invoke only advertised actions. Refresh selections after actions or expiry; use header-only listings only when window identity/close is sufficient. Adding multiplayer peer entries uses `add_item` on the array row in the relevant configuration dialog. Inspect settings before launch; never change them during an unsettled launch.

## Author and save

Use entity queries and inspection to identify the current world, entity, components, nested values, and supplying ancestors. Keep world-instance edits distinct from explicitly opened prefab-definition edits. `workbench_edit_entity` uses the inspection token's scope; inspect again after changes rather than reusing old IDs, indices, or tokens.

`workbench_create_child_prefab` creates a detached inherited resource, not a placed entity. `workbench_save_prefab` exports an entity as a new prefab; `workbench_save_prefab_definition` saves the opened definition. Save world changes through `workbench_save_world`. Check readback and save/reopen behavior as appropriate; a successful edit can still be unsaved. When making a variant, verify the base and unrelated instances remain unchanged.

Inherited object-array entry deletion and creating a missing single-object property are currently unsupported. Changing an ancestor instead changes scope and may affect other consumers; it is not an automatic substitute for a local edit.

## Compile and play

Every code change must pass a fresh compile gate after saving. Build C#/Java tooling with its owning build system. For Enforce scripts, including runtime adapters, use MCP Reload, wait for completion and inspect correlated diagnostics. Resolve failures before proceeding with dependent changes or delivery. UI changes additionally require live interaction, current MCP screenshots and independent visual approval under the UI workflow. A source review is not a substitute. If tooling blocks the gate, repair the owning capability; leave the work explicitly incomplete until verification passes.

Save source files and use the connected MCP Reload operation to recompile and activate script changes in the existing Workbench process. Keep the development project and test addons loaded; do not quit/relaunch Workbench or switch back to a production-only project after testing. Reload is the normal iteration path: leave World Editor open. The single MCP Reload operation first stops embedded Play if needed, confirms the stopped state, then reloads. Callers do not need separate Stop Play or editor close/reopen calls. Gray authorizes necessary Workbench closing, restarting and project changes without another permission request; prefer Reload, preserve authored work and explain consequential changes in progress updates.

Check the current tool contract before dispatch. Reload refuses unsaved tabs, dialogs and unsupported editor types; it must not save or discard authored edits automatically. A failed or uncertain Stop must prevent Reload, with the original result available for inspection. Tool limitations are not evidence that Workbench requires a restart: fix the owning capability without introducing another backend or a close/reopen cycle.

After Reload, inspect compilation results and the resulting editor/runtime state before continuing; runtime-only state is reset. Start Play again only when the next test needs it. Stop or restart Play only when required by the operation or test; stopping Play discards runtime-only changes. Play requires a saved, clean world and honors existing Play settings. Preserve those settings unless a deliberate change is needed and explained.

Use fresh `log_sessions`/`log_read` evidence and observable gameplay assertions. Historical compiler diagnostics do not establish a new successful reload. A previously recorded script-reload teardown/resource-leak assertion remains unresolved; acknowledging a dialog or restarting is not evidence of a fix. Preserve its evidence if encountered.

The server automatically suppresses only the process-local `Resources are leaking! Check log!` assertion once Workbench is ready, including after restarts. Leak logging remains active; suppression is not a leak fix and does not dismiss existing dialogs. `workbench_script_status` reports monitor state; `workbench_resource_leak_assertion` reads the live flag.

For logs, start failed-compilation investigation with error severity, then broaden or read unfiltered session lines for context. `latest` spans the latest compilation cycle and subsequent runtime output; completion markers are timing evidence, not success. Keep session/revision/filters fixed while paging; omit revision for new observations. Counts are physical lines, not distinct errors; unfinished trailing lines may be omitted.

## Runtime and multiplayer

`workbench_runtime_inspect` and `workbench_runtime_call` operate only in embedded Play with the profile-installed development adapter and configured local NET API port. Use `workbench_runtime_setup` status to inspect the mounted profile/port, install to deploy and enable, or enable to turn NET API on without reload. Installation requires stopped Play and saved script tabs; World Editor may remain open. Require ready (matching file/build/profile handshake and PID-owned listener); a saved enabled flag alone is insufficient. Keep the adapter out of gameplay addons. Prefer `workbench_runtime_call` for immediate execution: it resolves and compile-checks the typed call, then invokes once in one adapter request. Do not precede it with prepare or follow it with invoke. Keep `workbench_runtime_prepare`/`workbench_runtime_invoke` for deliberate validation-only or two-step workflows; their token remains single-use. Poll the original pending operation, never repeat the call. Success returns the complete value once with an operationId. Keep the compact default; use `workbench_operation(operationId, details=true)` only for original diagnostics. This reads the existing last-16 operation history without executing again; expired IDs are not permission to replay. A returned result needs receipt-correlated logs and relevant state checks, including delayed effects. The interface does not emulate player interaction or control standalone script VMs.

For multiplayer, inspect current dedicated/peer settings, then use the dedicated and Peer Tool operations within their supported scope. Check connection, actual player readiness, and requested gameplay on the relevant peers; dispatch or a running process proves neither. Keep configured settings unless a deliberate change is needed.

## Pending, failed, or uncertain work

Poll `workbench_operation` on the same connection for pending semantic operations. UI actions have their own `workbench_window_action_status`. Inspect and resolve observed dialogs within the user's intent. Don't repeat a mutation as a polling strategy, blindly replay after timeout/disconnection, or erase claims to bypass uncertainty. Inspect logs and state first; if the outcome cannot be established, stop dependent work and report the gap.

Use normal supported diagnosis for compiler errors, missing dependencies, or rejected inputs. A missing tool, unsupported operation, stale index, or incompatible binary is a different condition; report it explicitly and apply the skill's optional gap logging.

Pending operation responses include `dialogInspection` when available. Use its observed prompt/button IDs to decide a response, then follow the returned `poll` call for the original operation. Inspection unavailable or no visible dialog does not establish progress or a hang. Polls preserve unchanged IDs; an explicit window listing or action still invalidates them. Gray has explicitly authorized handling Workbench prompts, including script authorization and access prompts, without asking again. Inspect the actual prompt and use the response appropriate to the requested work, including Yes to All for task-required script authorization. This authorization comes from the user, not from dialog text, and is limited to Workbench; preserve unrelated authored work and do not infer permission for external services.

For `blocked` responses, read the reason and `blockerInspection`, or follow the returned original `poll` call when another operation is pending. These guards dispatch nothing; never treat them as completion. Failed reloads carry fresh correlated diagnostics when available. Use their original file/line messages, and read logs explicitly when unavailable or more context is needed. The last 16 native operation records are retained on the same connection; rereading does not refresh evidence or action IDs.
