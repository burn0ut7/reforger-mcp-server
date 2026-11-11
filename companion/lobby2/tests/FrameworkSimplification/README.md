# Framework simplification checks

Optional development tests outside the production addon. No automatic execution and no production dependency. The fixture constructs isolated data; it does not alter the live mission, permissions, players or resources.

Temporarily mount this directory's `scripts/Game` under the Workbench profile's `scripts/Game/FrameworkSimplificationChecks`, following the existing RespawnLocations test setup. Reload through Reforger MCP, start embedded Play, then invoke `GC_TestFrameworkSimplification()` on the scenario's `GC_LobbyManager`. Remove only the verified test junction and Reload afterward to restore the original script set. A test project depending on Lobby2 can load the same source normally.

Coverage:

- All condition kinds, including unknown kinds; null children, duplicate references, selected/empty change sets, recursion depth boundaries, non-group children and public overrides nested in groups.
- Parameter batches in arbitrary order, independent value copies, stable catalog storage, revision handling, self-aliased application and snapshots, stale snapshots and atomic rejection of duplicate/missing/null values.

Verified through live Reforger MCP on 2026-11-11, Workbench 1.9.0.37, Arland example: `checks=214 failures=0`. Runtime operation: `792b72c616e34ed5af1d22329e185752`.

The condition traversal consolidation was withdrawn because it bypassed nested public overrides. Restoring an older source file also restored its timestamp; an initial Reload reused the changed compiled version and failed both override checks. Updating the source timestamp and Reloading produced the passing result above. Preserve this coverage when considering future traversal changes.

The action validator's 21 switch cases were separately checked against their original branch bodies. These tests do not establish multiplayer, UI interaction, throughput or full-framework regression coverage.
