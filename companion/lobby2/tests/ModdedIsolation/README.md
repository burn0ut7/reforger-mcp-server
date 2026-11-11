# Modded-hook isolation checks

Temporary embedded-Play fixture; keep outside the production addon. Requires a fresh local Lobby2 session, the actual authenticated local admin, and a controlled character with an empty jacket slot. It spawns two temporary US jackets, changes the caller's clothing permission through the production authority check, performs native inventory operations, restores the permission, deletes both items, and shuts down the test controller presentation. Stop Play/Reload afterward; do not run during an ongoing mission.

Mount `scripts/Game` temporarily under the Workbench profile `scripts/Game/zzzModdedIsolationChecks`, Reload, start Play, and invoke `GC_LobbyManager.GC_RunModdedIsolation(stage)` on `GameMode_Lobby2` through MCP. Run stages 0, 1, 2, 3 in order in separate calls; inspect results before proceeding. Stage 3 must run after a queue tick. Remove only the verified junction and Reload afterward. The fixture casts to the final controller type because profile modded declarations precede addon extensions in this compilation context.

Checks cover native validation opt-ins remaining false; real clothing insertion, locked removal/replacement rejection, unlocked permission, invalid slot rejection, requester-zero rejection, repeated audit rebind/detach, native events while audit is detached, and repeated production presentation shutdown. Native queue removal is deferred: immediate `GetRemainingTime` can still report the original delay even after explicit `Remove`; verify cancellation in a later call.

The fixture's temporary `GC_TestAbsentFeature` disables only manager availability during one synchronous permission query, restores it before returning, and verifies native allowed/invalid results while Lobby functionality is absent. This is not a separate unrelated world, an addon-free process, or multiplayer coverage. Authority uses the connected test player's actual admin status; no roles are granted or spoofed.

Audit verification also requires inspecting `GC | Admin Action` output for the test jacket: one initial add, one first remove, no add while detached, and one remove after rebind. The existing audit formatter retains item prefab, replication ID, position and storage context. Native-event counters alone do not prove audit multiplicity.

## Recorded run (2026-11-11)

Workbench 1.9.0.37, embedded Arland Lobby, actual local admin and spectator body: **27 checks, zero failures**. Final result operation `7e83069c472849b6a2814c01d0851998`; fixture compilation `fdc8b7dccf6d41cbac4611ae6e7476c9` had no script errors. Session `logs_2026-11-11_15-40-35`, lines 28871/28874/28877 contain exactly the expected jacket add/remove/remove audit sequence, preserving prefab, replication ID, position and storage. No audit add occurred while detached. Temporary items were deleted, permission restored, the test junction removed and production scripts reloaded. Existing resource GUID diagnostics are unrelated and remain unresolved.

## Native provenance

Installed game/Workbench 1.9.0.37; PAK revision `c52648a62fcb4229ad089610d060ea44`. `game/data/data007.pak/scripts/Game/Inventory/ScriptedInventoryStorageManagerComponent.c:7–23` owns the native inventory invokers. `Inventory/SCR_InventoryStorageManagerComponent.c:531–551` calls its inherited event handlers. `generated/InventorySystem/BaseInventoryStorageComponent.c:113–122` documents cached override switches.

Workbench binary SHA-256 `456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28`; analysis key `4b5e2a65022388040dcc1dc4a50629d2466c4e260a927bbbc73617472251e84e`:

- `0x140c67760` maps CanRemove/CanReplace script entries to `0x143bf60a4`/`0x143bf60a8`.
- `0x140c680f0` checks the cached remove override; when false, it first calls native validation at virtual offset `0xc8`, then invokes the script permission. Code SHA-256 `4ac37896e0e10361a787a88311847d894a46b63f1d760c0b3ff1ce995a6fd962`.
- `0x140c68160` follows the corresponding replace path, native offset `0xd0`, then script permission. Code SHA-256 `5b6344d0638d3baca4a337302ccddfdbd3bb00e0ea1a544213dc22a10a2d3499`.

These are inferred native implementations, not supported script APIs. The live checks establish that retaining the default switches preserves the clothing lock in this installed build; no native addresses are shipped or called by production code.

Independent review found the manager's shutdown previously reached only the local controller. The follow-up traverses native connected-player controllers on authority and calls the existing cleanup chain for remote controllers, retaining local cleanup once. The 27-check run preceded this traversal change; subsequent production compilation verifies it, but there was no connected remote client to exercise that branch live.

Remaining coverage: dedicated/multi-client behavior (including remote-controller shutdown), an addon-free process baseline, full world transitions, late replication and mod combinations. Native validation is no longer intentionally bypassed by these two global switches. AI firing and map-recovery hooks were deliberately retained: optimized muzzle events omit the required projectile, and removing cross-mission recovery invalidation would lose its safeguard.
