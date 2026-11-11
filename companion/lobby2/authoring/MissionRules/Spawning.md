# Spawn Action prototype

Add **Spawn Action** under a mission event's Actions, then choose one Spawn setup:

| Setup | What it adds |
| --- | --- |
| Prefab | Creates the selected prefab at a named entity's position and direction. |
| AI Group | Creates a non-playable AI group and appends named AI waypoints in order. |
| Crewed Vehicle | Creates a vehicle and fills its available seats using native compartment spawning. |

All three share Prefab, Location and optional Entity Name. Location must name an existing entity. Entity Name must be unused; leave it empty for repeatable spawns. Placement is exact: there is no ground snapping, collision search or random offset. Prefabs keep their own normal component behavior and replication configuration.

The event owns when to spawn: conditions, repeatability and Advanced Delay. Other actions in the same event remain the place for follow-up framework actions. AI waypoints are orders, not a nested mission-action framework. AI Group prefabs must be configured to spawn their members; playable groups belong to Lobby2's slot system and are rejected.

## Vehicle crew

An empty Crew Groups list fills driver, gunner and passenger seats with the vehicle's default characters in one AI group. A seat without a default character stays empty. A Crewed Vehicle must create at least one crew member; use Prefab for an empty vehicle.

Add a Crew Group only for customization. Each group has Driver, Gunners and Passengers checkboxes, an optional character list, optional waypoint names and Keep Gunners Seated. Groups are processed in order. Each available seat can be claimed once. The engine determines seat order, including secondary compartments.

- An empty character list uses the vehicle's default crew.
- A populated character list fills matching seats in order. A blank entry deliberately reserves an empty seat. When the list ends, this group stops filling seats; later groups may use the remaining seats.
- Put driver/passengers in one group with movement waypoints, and gunners in a second group with no waypoints, to give them independent orders.
- Keep Gunners Seated prevents voluntary combat dismounting; it is not a promise that a destroyed vehicle retains its crew.

Example: select Crewed Vehicle, choose a transport prefab, and set Location to `reinforcement_spawn`. Leave Crew Groups empty for default crew. To create only driver and gunners, add one Crew Group and uncheck Passengers.

## Execution and limits

The existing manager gate requires the active Lobby2 server and its running framework instance. Names are resolved directly when the action executes. Native groups own membership after spawning. Crew creation is synchronous through native compartment methods; there is no repeating timer or pending job after mode exit.

Keep Gunners Seated uses a Lobby2-owned combat-component override, independent of TilW Mission Framework. Only explicitly configured spawned gunners in their originating Lobby2 game-mode instance suppress voluntary combat dismounting. Other characters and modes retain the inherited decision; old mode ownership is cleared when it no longer matches. This adds no RPC, registry, spawn hook, frame callback or world scan.

Resource and waypoint checks happen before spawning. A failed setup removes the newly created root, crew and crew groups tracked by this attempt. Existing named entities/groups are never replaced or merged. This is cleanup, not a transactional undo of arbitrary third-party prefab component side effects. Successful spawns remain ordinary world entities; resetting an event does not delete them.

Bounds: 16 crew groups, 128 compartment slots, 128 characters per group and 128 waypoints per list. Malformed settings reject the action. Missing resources/locations, name collisions and setup failures are reported through the existing framework action-rejection log with the location as context. There are no automatic retries.

## Reference and prototype scope

Reviewed TilW commit `4a5300f94fb9323ce4942ef296bcffcdda922626`: [spawn instructions](https://github.com/Til-Weimann/tilw-mission-framework/blob/4a5300f94fb9323ce4942ef296bcffcdda922626/scripts/game/TILW_Instructions.c) and [crew configuration](https://github.com/Til-Weimann/tilw-mission-framework/blob/4a5300f94fb9323ce4942ef296bcffcdda922626/scripts/game/TILW_CrewConfig.c). Shared concepts are named placement, optional output name, AI waypoint orders, default/custom crew, seat filtering and independent crew groups. The GC implementation uses one action and a flat crew-group list.

TilW's deeper group/station nesting, joining existing named groups, custom group display names, separately delayed waypoint assignment and automatic idle-gunner splitting are not included in this prototype. Separate crew groups provide explicit independent orders. Event Delay controls the spawn itself. These omissions avoid hidden group ownership changes and extra timing systems while the core design is evaluated.

Native API evidence: installed game catalogue `c52648a62fcb4229ad089610d060ea44`, `scripts/GameCode/Vehicle/BaseCompartmentSlot.c` lines 315-410; `Prefabs/AI/Groups/Group_Base.et`; `scripts/Game/Entities/SCR_AIGroup.c` (playable default false); `scripts/GameLib/gameLib.c` EntitySpawnParams. World transform mode is set explicitly.

Validation: Workbench script Reload, source review of dispatch/placement/group ownership/seat consumption/cleanup, and optional `GC_SpawnActionTests.RunSpawnValidation` fixtures. Fixtures compile but have not been executed. No in-game test was run at the user's request. Actual vehicle initialization timing, seat population, replication and AI movement remain runtime verification work. The Workbench 1.9 picker UI was not independently verified.
