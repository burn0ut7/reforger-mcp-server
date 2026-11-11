# Respawn location checks

Optional development addon depending only on Lobby2. Nothing runs automatically.

In embedded Play, invoke these methods on the scenario's `GC_LobbyManager` using Reforger MCP:

1. `GC_TestRespawnLocations()` checks transforms, grid distribution, moving/rotating/deleted anchors, invalid configuration, faction-default replacement/reset/atomic rejection, action compilation, foreign framework-source rejection and an actual character spawn through the production respawn owner. Use a pre-match scene with an empty playable slot.
2. In a fresh pre-match local session, `GC_PrepareLocationWaveTest(false)` selects an available role through the normal slot handler and queues match start. Call `GC_FinishLocationWavePreparation()` after staging completes; it configures two tickets and a named default destination.
3. Call `GC_KillLocationWavePlayer()`, allow the normal death handler to reach KIA, then queue `GC_RunLocationWaveCase(-1)`. Call `GC_CheckLocationWaveCase()` after completion to verify the unavailable override preserves the old body and refunds the ticket.
4. Queue case `0` and check completion to verify the default anchor moved after acceptance, actual respawn position, native possession and ticket consumption.
5. Kill again, queue case `1` and check completion to verify a fixed one-wave override.
6. Kill again, queue case `2` and check completion to verify unticketed respawn at the original position despite the configured faction default.
7. Kill again and call `GC_TestWaveCancellation()` to queue a wave and end the match before execution. It verifies cancellation without changing the body or tickets.

Each queued case asserts that acceptance itself does not spawn or spend tickets. `PENDING` from a check means execution has not completed yet. The asynchronous follow-up passed all four cases and phase cancellation on 2026-11-11; stale-claim replacement and disconnect during a queued wave were not exercised. The historical results below describe the earlier synchronous implementation.

The same fixture supports `GC_RunIndividualLocationCase(0..3)`, with a normal death/KIA transition before each case. Cases 0 and 1 exercise authenticated slot Heal/Respawn and selected-player admin recovery at the faction default. Cases 2 and 3 exercise explicit current/original admin overrides. Each first calls the production admin handler with requester zero and asserts rejection without mutation. These cases require the actual local player to be authenticated as admin; they do not grant or simulate permissions. Run them before the three wave cases so the latter can still start with two tickets.

The wave fixture deliberately changes runtime mission state and kills/respawns the local test player. Stop Play afterward; it makes no editor/world-file changes. Do not run it in an ongoing multiplayer mission. It does not establish dedicated-server replication or multi-player collision avoidance.

For this session, the test addon's Game script folder was temporarily mounted at the Workbench profile's `scripts/Game/RespawnLocationChecks` using a directory junction. This lets Reload compile the separate test source without changing production dependencies or restarting the editor. Remove only that verified junction and Reload after the checks. Normal development can instead load this addon as a test project.

Verified on 2026-11-11 with Workbench 1.9.0.37 and the Arland example through live MCP:

- `GC_TestRespawnLocations`: `checks=20 failures=0`, repeated after cleanup corrections.
- Case 0: `passed=1 spawned=1 tickets=1 distance=0`; unavailable override preserved the player/tickets, then the moved faction default succeeded.
- Case 1: `passed=1 spawned=1 tickets=0 distance=0`; fixed override took priority over the faction default.
- Case 2: `passed=1 spawned=1 tickets=0 distance=0`; explicit original placement ignored the faction default without spending tickets.

Each successful wave also checked native possession and that the slot was no longer KIA. Independent review approved production and test changes. This is local connected-player coverage; dedicated/multi-client replication, collision-free placement and forced spawn/possession-failure refunds were not exercised. Existing Arland resource GUID warnings remained outside this change; the changed scripts compiled successfully.

All-respawn follow-up, 2026-11-11: the shared owner now resolves destinations for individual recovery as well as waves. Repeated live checks returned `checks=22 failures=0`, including individual default selection ahead of current-position fallback and preserving the previous body on a missing destination. All four individual admin cases returned `passed=1`, with authenticated execution and rejected requester-zero execution. All three wave cases passed again with exact destination positions and expected tickets; case 0 now exercises reservation/refund on rejected location resolution. Dedicated/multi-client coverage remains unperformed.

Historical briefing coverage: start a fresh session with `GC_PrepareLocationWaveTest(true)` and finish preparation after staging. Kill the body, then call `GC_CheckBriefingLocation(false)`; delete its replacement using `GC_DeleteBriefingTestBody(false)` and check again. Both earlier live cases returned `briefing passed=1 distance=0`. Deleting the anchor and body with `GC_DeleteBriefingTestBody(true)`, then `GC_CheckBriefingLocation(true)` returned `missing passed=1`. That earlier synchronous check also exercised initial-spawn catch-up. Catch-up now queues its work, so the immediate result alone no longer establishes its later outcome; that briefing regression needs a second observation after the callback. These checks exercise the real destruction/deletion callbacks.
