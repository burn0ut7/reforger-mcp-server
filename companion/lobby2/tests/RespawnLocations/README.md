# Respawn location checks

Optional development addon depending only on Lobby2. Nothing runs automatically.

In embedded Play, invoke these methods on the scenario's `GC_LobbyManager` using Reforger MCP:

1. `GC_TestRespawnLocations()` checks transforms, grid distribution, moving/rotating/deleted anchors, invalid configuration, faction-default replacement/reset/atomic rejection, action compilation, foreign framework-source rejection and an actual character spawn through the production respawn owner. Use a pre-match scene with an empty playable slot.
2. In a fresh pre-match local session, `GC_PrepareLocationWaveTest()` selects an available role through the normal slot handler and starts the match through the existing server phase/start owners. It configures two tickets and a named default destination.
3. Call `GC_KillLocationWavePlayer()`, allow the normal death handler to reach KIA, then call `GC_RunLocationWaveCase(0)`. It verifies that an unavailable override does not spend tickets or respawn the player, then moves the default anchor and checks respawn position, native possession and ticket consumption.
4. Kill again and run case `1` to verify a fixed one-wave override.
5. Kill again and run case `2` to verify unticketed respawn at the original position despite the configured faction default.

The wave fixture deliberately changes runtime mission state and kills/respawns the local test player. Stop Play afterward; it makes no editor/world-file changes. Do not run it in an ongoing multiplayer mission. It does not establish dedicated-server replication or multi-player collision avoidance.

For this session, the test addon's Game script folder was temporarily mounted at the Workbench profile's `scripts/Game/RespawnLocationChecks` using a directory junction. This lets Reload compile the separate test source without changing production dependencies or restarting the editor. Remove only that verified junction and Reload after the checks. Normal development can instead load this addon as a test project.

Verified on 2026-11-11 with Workbench 1.9.0.37 and the Arland example through live MCP:

- `GC_TestRespawnLocations`: `checks=20 failures=0`, repeated after cleanup corrections.
- Case 0: `passed=1 spawned=1 tickets=1 distance=0`; unavailable override preserved the player/tickets, then the moved faction default succeeded.
- Case 1: `passed=1 spawned=1 tickets=0 distance=0`; fixed override took priority over the faction default.
- Case 2: `passed=1 spawned=1 tickets=0 distance=0`; explicit original placement ignored the faction default without spending tickets.

Each successful wave also checked native possession and that the slot was no longer KIA. Independent review approved production and test changes. This is local connected-player coverage; dedicated/multi-client replication, collision-free placement and forced spawn/possession-failure refunds were not exercised. Existing Arland resource GUID warnings remained outside this change; the changed scripts compiled successfully.
