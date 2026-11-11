# Delete Entities and Assign Waypoints

These are separate actions alongside Spawn Action. Add them directly to an event's Actions list. The event still owns conditions, delay and repeatability.

## Delete Entities Action

**Entities** lists exact entity names. Each matching entity and its attached children are removed through the engine's replicated deletion helper. Missing/already deleted names are skipped, and duplicate targets are processed once. A list containing the active game mode or one of its parents is rejected before anything is deleted.

This is named-entity removal, not a prefab search or an AI-group member search. Naming an AI group does not explicitly enumerate its soldiers; engine lifecycle behavior still applies. Deleting a vehicle can affect occupants through its normal engine cleanup. Separately owned building interiors are not specially enumerated. Use this for intentionally named mission objects, with those ownership boundaries in mind.

Example: Entities = `old_truck`, `temporary_barrier`. Repeat execution skips objects already gone. An entity later spawned with one of those same names is a new matching target on the next execution.

### Radius selection

Add **Radius** to Delete Entities Action to use spatial selection instead of Entities. Choose an existing **Center** entity and a **Radius** in meters (0.1–1000). The center and active game mode are protected; a loose center inside a building does not protect the whole interior. Selection uses inclusive 3D distance to each entity's origin.

Both prefab lists are optional. An empty **Whitelist** permits all types, including entities without prefab data. **Blacklist** protects matching prefab types and wins over the whitelist. Both include inherited prefab variants, resolved from prefab containers without requiring hierarchy components. Invalid filter resources reject the action before deletion.

Buildings and loose interiors are handled together. Existing building interior volumes are read from prefab component data; buildings without configured interior volumes use their oriented bounds. Objects whose origins lie inside these volumes join the building's deletion, including objects outside the radius or whitelist. No reparenting or base-component overrides are used. Blacklisting a building protects its contents, including when only its interior overlaps the radius. Blacklisting an interior object protects the whole building group. Protection also propagates through overlapping groups. Other building entities are not treated as furniture.

This is spatial association, not proof of ownership: independently placed objects inside a building volume are included, and objects outside its volumes are not inferred to belong to it. Attached children follow normal engine deletion; protected attachments prevent deleting their parent. Named-only deletion retains its existing behavior above.

The action uses bounded AABB/OBB queries, never sphere queries or recurring scans. Discovery follows overlapping buildings beyond the initial radius before resolving protection. Limits are 4,096 initial candidates, 128 discovered buildings, 64 volumes per building, 4,096 interior query visits per building, and a shared 16,384 interior/protection budget. Exceeding a limit rejects the action before mutation. Split very large cleanups into smaller areas.

Replicated entities use native replicated deletion. Nonreplicated map objects use recorded world entity IDs with class, prefab and position guards, broadcast to peers and serialized for joining clients. The retained history is limited to 16,384 records per mode instance; exceeding it rejects the action before mutation. Independently spawned client-local objects with unrelated IDs are not matched by proximity.

## Assign Waypoints Action

- **Group Name**: exact name of an existing AI group, not a vehicle or individual soldier.
- **Waypoints**: existing AI waypoint names, in the desired order.
- **Replace Existing**: unchecked appends orders; checked removes current assignments before adding these orders. Checked with an empty list clears the group's waypoint orders.

Every new waypoint resolves before any existing assignment is removed. A missing group, missing waypoint or wrong entity type rejects the action without changing orders. Empty append lists are invalid. Clearing orders does not disable AI combat reactions.

The engine may delete group-owned editable waypoints when their assignments are removed. Replacement therefore rejects an already assigned, group-owned editable waypoint in the new list before changing any orders. Use new waypoint entities for that replacement. Ordinary authored waypoints without this editable ownership can be reused. This keeps native waypoint ownership intact without global overrides or temporary reparenting.

Example: spawn an AI group with Entity Name `reinforcements`. A later event assigns `rally_point`, then `defend_base` to Group Name `reinforcements`. Check Replace Existing to replace that group's previous route.

## Implementation and verification

Both actions use the existing current-framework/active-Lobby2/server authority gate. There are no client-request RPCs, polling loops or delayed callbacks. Named lists are limited to 128 nonblank names; radius prefab filters allow 128 resources each. Names resolve at execution, supporting entities spawned earlier in the mission. Spawn Action and Assign Waypoints share the same waypoint implementation.

Reviewed TilW `TILW_DeleteEntitiesInstruction` and `TILW_AssignWaypointsInstruction` at commit `4a5300f94fb9323ce4942ef296bcffcdda922626`. Installed engine evidence, catalogue `c52648a62fcb4229ad089610d060ea44`: `scripts/Game/Helpers/SCR_EntityHelper.c:174-179` delegates deletion to `RplComponent.DeleteRplEntity`; `scripts/Game/Commanding/Commands/SCR_StopMovementGroupCommand.c:18-23` removes existing waypoint assignments. `scripts/Game/Editor/Components/EditableEntity/SCR_EditableGroupComponent.c:214-221` can delete removed group-owned editable waypoints, and `scripts/Game/Entities/SCR_AIGroup.c:2401` explicitly documents deletion by removal listeners.

Original named-delete/waypoint verification was source review and Workbench compilation/reload. Optional `GC_EntityActionTests.RunEntityActionValidation` contains ten validation checks; it compiled but was not executed in that task. Actual AI movement and picker presentation remain untested.

Radius deletion passed 22 checks in embedded Play through `GC_TestRadiusDeletion` in the optional test addon: real mesh-only entities, inherited filters, exact radius, authority rejection, repeated deletion, static-record identity guards, loose furniture with a plain Building bounds fallback, a rotated stock barn with configured interior volumes, and overlapping blacklist protection beyond the initial query. The production handler removed the actual entities. Workbench 1.9.0.37 reload produced no script errors; test operation `b5130b865a994a4a865ceb28ad6d4171` returned zero failures. Connected-client broadcasts and fresh-client JIP have not been exercised; the current MCP profile reports the multiplayer settings/status capability unsupported. Local record tests do not establish cross-peer identity or snapshot timing. Native static-ID snapshot precedent is `SCR_CompositionSlotManagerComponent.c:301–312`; interior volume evidence is `SCR_DestructibleBuildingComponent.c:829–876` in the installed catalogue above.
