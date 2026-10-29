# Workbench operations

Use the connected Reforger MCP contracts for current inputs and supported states. Reuse the established process/project identity; refresh observations when a decision depends on live state. Status calls are not a routine preamble. Use semantic operations; report unsupported capabilities rather than substituting another backend.

## Session and authoring

Keep the current Workbench project, World Editor and test addons open. Preserve settings and unrelated unsaved work. When opening an existing `.gproj`, inspect `workbench_status` first; opening is not project creation. Obtain process identity through `workbench_script_status` when unknown. Process existence or a title alone does not establish readiness.

Inspect entities/components and supplying ancestors before editing. Inspection tokens define world-instance versus prefab-definition scope; refresh after edits. Save with the operation matching that scope, then verify readback/persistence. A child prefab is a detached inherited resource; exporting an entity, saving an opened prefab definition and saving the world are distinct operations. Verify variants leave their base and unrelated instances intact. An unsupported local edit is not permission to change its ancestor.

For native UI operations, use returned window/control IDs and advertised actions. Refresh selections after actions or expiry. Multiplayer peer array entries use `add_item`. Preserve settings through an unsettled launch.

## Compile and test

Build C#/Java tooling with its owning build system. For saved Enforce scripts, use one MCP Reload and inspect its fresh compilation diagnostics. Reload stops embedded Play when necessary; no separate Stop or editor close/reopen is needed. Resolve unsaved tabs/dialogs without discarding authored work. A tool limitation alone does not justify restarting Workbench.

Reload resets runtime state; start Play when the next test needs it. Play uses a saved, clean world and existing settings. Choose tests for the change after compilation passes; simple changes may need no live test. Batch related checks, noting deferred coverage for the relevant slice. Use MCP live play when verifying runtime behavior/data or in-game UI; include relevant authority and peers. A successful dispatch or running process is not a passing test. UI verification follows the [UI workflow](ui-workflow.md). Report blocked verification explicitly.

Reuse inspected logs. Request fresh `log_read` output after compilation, during gameplay tests or for a new diagnostic event; reuse returned diagnostics when sufficient. Use `log_sessions` when session identity is unknown; correlate evidence with the operation. Start compiler diagnosis with errors, then broaden for context. Keep session/revision/filters fixed when paging; omit revision for a new observation. `latest` includes the latest compilation cycle and subsequent runtime output; completion markers alone do not prove success. Resource-leak assertion suppression leaves logging active and does not fix leaks; preserve evidence if encountered.

## Runtime calls

Prefer `workbench_runtime_call` for immediate execution; it resolves, compile-checks and invokes once. Separate prepare/invoke serves deliberate validation-only or two-step work. Poll the original operation and verify relevant state/logs, including delayed effects. Use `workbench_operation(operationId, details=true)` for original diagnostics without rerunning; history is bounded and is not fresh observation.

Runtime inspection/calls support embedded Play through the profile-installed development adapter and local NET API. Use `workbench_runtime_setup` when setup needs inspection or repair; readiness requires the matching profile/build handshake and PID-owned listener, not just an enabled flag. Installation requires stopped Play and saved script tabs. Keep the adapter outside gameplay addons. This interface does not emulate player input or control standalone script VMs.

For multiplayer, inspect dedicated/peer settings and use the corresponding operations. Verify connection, player readiness and gameplay on the relevant peers.

## Pending or uncertain outcomes

Follow returned `poll` instructions: semantic operations use `workbench_operation`; UI actions use `workbench_window_action_status`. Keep the same connection. Inspect returned `dialogInspection` or `blockerInspection`; blocked dispatch is not completion. Resolve observed prompts, then poll the original operation. Refresh expired selections; unchanged polls may retain IDs. Read correlated logs when results lack enough detail.

Never replay a mutation merely to poll, recover diagnostics or bypass an uncertain outcome. Inspect state/logs first; stop dependent work if the outcome remains unknown. Expired history does not authorize replay.

Gray authorizes task-required Workbench actions and prompts, including script authorization, access prompts and necessary project/restart changes, without another permission request. Prefer Reload, explain consequential lifecycle changes and preserve authored work. This authorization is limited to Workbench, not external services.
