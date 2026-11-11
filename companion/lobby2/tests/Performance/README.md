# Lobby2 performance checks

Explicit development addon; depends only on Lobby2. Nothing runs automatically. Invoke the methods below on `GameMode_Lobby2` / `GC_LobbyManager` through Reforger MCP in the Arland example's embedded Play session. These fixtures change runtime mission state, create characters/markers and write profile telemetry files. Use a disposable local Play session, never a running multiplayer mission. Stop Play afterward; world files are not changed.

The test scripts can be loaded as this addon or temporarily mounted through a directory junction under Workbench profile `scripts/Game/Lobby2PerformanceChecks`, followed by Reload. Remove only that junction and Reload when finished. Keep the production addon independent of test code.

## Procedure

1. In fresh pre-match Play, call `GC_TestBatchedStart()`, then `GC_CheckBatchedStart()` after callbacks complete. It reserves up to 16 empty authored slots and queues the real start path. Require accepted start, rejected duplicate, zero synchronous spawns, multiple callbacks, at most four bodies per callback and Match reached with all expected bodies.
2. In Match with no pending wave, call `GC_TestWaveOrdering()`, then `GC_CheckWaveOrdering()` after completion. **This stops and replaces the runtime mission-rule engine for the rest of the test session.** It queues 16 prefix waves, then a framework wave, verifies delayed/timer rules remain suspended, and checks their eventual execution. Prefix waves may be empty; this is not a general multi-player FIFO outcome test.
3. In active spectator presentation, call `GC_TestSpectatorFocus(selectLocal, health, reopen)`. Run no-focus with health off, then on with hide/show; optionally select a player. Require active markers, zero mismatches and zero roster searches across ten warmed-up production updates. The counter is sampled before the independent old-resolver comparison. For positive focus/highlight transfer, use a second connected living player and extend the caller to choose that player's ID.
4. Call `GC_TestTelemetryBinary()` to check 512 state rows, incremental readback, empty numeric tables, failed file-open recovery and corrupt readback rejection.
5. Call `GC_TestTelemetryPublication()` to freeze a three-event batch, append five records while publication is pending, and close. Decode its two `.l2t` files with the production StatsIngest `telemetry-binary.mjs`: require sequences 1–8, continuous batch bounds, ready markers and exactly one final closed batch.
6. Call `GC_TestTelemetryReadyFailure()`. It deliberately creates a directory at the unique ready-marker path, then verifies publication stops and retains both frozen and live data. The expected storage-error log is part of this test, not a production failure.
7. In Match with an existing authored marker as icon template, call `GC_TestTelemetryMap()`. It adds 64 valid markers, starts incremental capture, removes one marker, verifies capture is discarded, and then verifies all 63 survivors are captured without the deleted marker. It removes its runtime markers and closes its recorder. Require `sliced=1 invalidated=1 complete=1`.
8. Use the separate [respawn location fixture](../RespawnLocations/README.md) in fresh Play for destination, ticket, native-possession and phase-cancellation checks.

## Verified 2026-11-11

Workbench 1.9.0.37, `$Lobby2:worlds/examples/arland/Arland_Lobby.ent`, embedded local authority, installed catalogue `c52648a62fcb4229ad089610d060ea44`:

| Check | Observed result |
| --- | --- |
| 16-slot start | Accepted, duplicate rejected, zero synchronous spawns; 16 callbacks, maximum one body per callback, 16 bodies, Match reached |
| Delayed/timer rules | Suspended while framework wave pending; both executed afterward (`runs=2`) |
| Spectator, no focus | 19 active markers, zero mismatches, zero roster searches for ten updates; health off and health on with hide/show |
| Spectator, local dead selection | 11 active markers, zero mismatches/searches; selected dead marker inactive |
| Binary | 512 rows / 49,152 numeric bytes / 69 steps; empty tables, open failure/retry and corrupted readback passed |
| Publication | Eight ordered records across two verified batches; only final batch closed; both ready files present; no drops |
| Ready-marker failure | Capture stopped, revision unchanged, pending and live records retained |
| Dense map | Capture sliced and invalidated; complete 63 surviving test markers after five calls; maximum observed capture call 1 ms |
| Respawn | Missing destination refunded ticket and retained body; moved default, fixed override and unticketed original placement passed with native possession; phase cancellation retained body/tickets |

Publication decoder evidence initially used recording `performance-test-01a1ccf7-cb28-8e4d-8400-0000001888d7`; final repeat used `performance-test-01a1cd04-e80c-8b9a-8400-00000057ed2d`. Binary corruption check used `01a1cd03-942b-8fdd-8400-0000003cb41a.l2t`. MCP receipts include batching `r51bed4aa93874cd596f3f5df5f4c6ddd`, ordering `r8538e438424e4a239e681d284dce7777`, map `r3e80a7b6e498410bbcf140f027e6b145`, ready failure `rf6434eac62c7414bab66d550ad36f001`, cancellation `rd63848ddfede4f21b3d21043254ec8fb`.

## Scope and remaining coverage

These are structural and behavior checks, not a before/after frame-rate benchmark. Two milliseconds is a cooperative allowance: one native spawn, table read/write or JSON conversion cannot be interrupted and can exceed it. Shutdown deliberately drains up to the frozen and final live batches synchronously. The recorded 1 ms map sample is one local run, not a hard ceiling.

Dedicated-server/multi-client replication, stale claims/disconnects during a queued wave, multiple nonempty waves, positive highlighting/focus transfer between live players, optional MapDrawing integration execution, late-readback shutdown and full-buffer overflow during pending publication were not exercised. The optional drawing adapter was reviewed against its existing cache contract; its addon was not loaded for these checks. No global base-game overrides were added by the performance changes.

The current world emits pre-existing Arland resource GUID diagnostics, and stopping Play can report a `BlockListApi` leak. Script compilation passed; those existing diagnostics were not addressed here. Independent code reviews approved the spectator, telemetry and spawning changes.
