# Strict modded-class review

## Implemented follow-up (2026-11-11)

Production commit: Lobby2 `384d6bd` (`Reduce global inventory overrides`). Independent source review approved the final changes. Final production-only Reload `ebdcd38aed574f84910448e7c3385fd5` reported no script errors; three pre-existing Arland world resource GUID errors remain.

Removed four shared overrides without removing features: native inventory invokers now feed admin auditing through the Lobby controller's possession/shutdown lifecycle, and the two cached clothing override switches no longer bypass native validation. Clothing permissions still apply after native checks. JIP input and queued presentation work now detach at explicit presentation shutdown as well as destruction. Shared declarations decrease from 39 to 38; the inventory type still has a separate Workbench hook.

Embedded Play passed 27 checks with zero failures, including locked native remove/replace rejection, unlocked permissions, invalid-slot rejection, unavailable-manager fallback, repeated subscription changes and repeated shutdown. Audit logs show exactly one initial jacket add and two removals, with no add while detached. Independent review also exposed that manager shutdown reached only the local controller; it now traverses the native player registry on authority to clean up remote controllers before destruction. That remote branch is compile/source-verified, not live multiplayer-tested. See [fixture, native provenance and coverage limits](tests/ModdedIsolation/README.md). This is not addon-free, full world-transition or multiplayer certification.

Retained AI muzzle hooks: optimized events omit the projectile, while replacing the ordinary hook would require new weapon/muzzle lifecycle tracking absent from the aggregate statistics owner, including swaps and turrets. Retained recovery invalidation because its narrowly scoped cross-mission cleanup prevents stale recovery. Arbitrary external player prefabs prevent a local clothing-storage subclass migration from covering every supported body.

## Historical audit

Reviewed 2026-11-11 against Lobby2 `6a1673a`, including its current working tree. This is a source and installed-engine-contract review, not multiplayer or cross-mode certification. No gameplay code was changed in this review.

## Result

Production scripts contain **75 modded declarations**: **39 declarations extending 36 base-game types**, and **36 extensions of Lobby2-owned GC types**. Repeated declarations extend the same class; they are not additional component instances. Global hooks are spread across feature directories, `Game/modded`, and `GameCode/Network`.

Most behavior-changing hooks have active mode/feature checks and preserve inherited behavior outside that scope. This does not establish that every global hook is necessary. **Strict necessity and isolation are not fully approved:** there is a clear native-event replacement, a lifecycle cleanup gap, and cached inventory overrides need stronger isolation evidence.

## Findings

### 1. Clothing overrides change native dispatch globally

`Loadout/GC_ForcedClothingStorage.c:28–36` unconditionally returns true from `OnOverrideCanRemoveItem` and `OnOverrideCanReplaceItem`. Actual permission callbacks check Lobby ownership and otherwise call `super`, but native callback dispatch remains changed in unrelated modes.

Installed `BaseInventoryStorageComponent.c:113–122` explicitly says these opt-ins are cached and cannot be dynamic. Its script defaults for `CanRemoveItem` and `CanReplaceItem` return true (`:139–142`); the inspected `SCR_CharacterInventoryStorageComponent` has no active script implementation of those methods. Calling `super` therefore does not itself demonstrate equivalence to the untouched C++ permission path. This is a **validation gap, not a demonstrated inventory bypass**.

Required: compare valid/invalid removal and replacement with the addon absent, loaded in an unrelated mode, and active in Lobby2, including transitions and client/server authority. Investigate an opt-in storage subclass/resource or native permission mechanism. Do not insert a dynamic mode check into the cached opt-in callbacks: initialization order could silently disable protection.

### 2. Admin inventory auditing can use native events

`Admin/GC_AdminGearAudit.c:55–69` overrides global item-added/item-removed callbacks. Native `ScriptedInventoryStorageManagerComponent.c:7–23` already exposes `m_OnItemAddedInvoker` and `m_OnItemRemovedInvoker`, carrying item and storage. The native SCR handlers call their inherited implementations (`SCR_InventoryStorageManagerComponent.c:531–551`). Lobby2 already uses these events in `Player/GC_BoundaryDetectionController.c:219–230`.

Replace the audit interception with subscriptions owned by the Lobby player/inventory lifecycle. Rebind on controlled-entity changes; detach on replacement, disconnect and shutdown. Preserve current server, connected-player and authenticated-admin checks at execution time, with exactly one record per successful mutation. Verify respawn and admin login/logout. The current removal audit runs before `super`; native event notification runs inside the inherited chain. Check that item/storage lifetime, audit timing and recorded context remain equivalent. Keep this responsibility separate from boundary detection.

This removes two global callback overrides; the separate Workbench-only `IsAnimationReady` hook on the same native type remains a distinct decision.

### 3. JIP cleanup waits for destruction instead of mode shutdown

`Player/GC_JipController.c:15–23` installs its input listener during initialization. Its destructor (`:175–183`) removes that listener and the presentation/movement-observation callbacks, but it has no `ShutdownLobbyPresentation` override. Other Lobby presentation features use that explicit shutdown boundary before destruction.

If the controller survives mode exit, the input listener remains registered until destruction, and pending callbacks are not explicitly cancelled at presentation shutdown. Those callbacks use nonrepeating scheduling and stop rescheduling when they execute without an active mode. The input callback also checks the active mode, so this is a confirmed cleanup omission, **not proof that it teleports players in unrelated modes**. Move idempotent cleanup into the normal presentation shutdown chain, preserve `super`, and retain safe destruction handling. Validate shutdown before destruction and repeated shutdown.

### 4. AI firing hooks have different replacement prospects

`Stats/GC_AIWeaponEvents.c` preserves `super` for both ordinary and optimized muzzle effects, then calls the active statistics owner. Recording rejects inactive collection, invalid projectiles and player-fired shots.

Ordinary `SCR_MuzzleEffectComponent.GetOnWeaponFired()` supplies effect entity, muzzle and projectile (`GameCode/Effects/SCR_MuzzleEffectComponent.c:5–24`). An owner-scoped subscription is a candidate, provided it covers spawning, weapon/muzzle changes, turrets and deletion without introducing a more complicated polling registry.

The optimized component's getter returns `ScriptInvokerVoid` (`SCR_OptimizedMuzzleEffectComponent.c:48–53`), omitting the projectile required by `GC_MissionStatsComponent.RecordAIFired`. Do not assume both hooks can be replaced using the same event contract.

### 5. Recovery deliberately performs cleanup outside Lobby2

`modded/GameMode/SCR_BaseGameMode_M.c:7–14` invokes `GC_MapPlans.NoteMission` after inherited initialization on every non-editor server mode. In unrelated modes, `Briefing/GC_MapPlans.c:20–38` invalidates an existing Lobby2 recovery eligibility record. It does not record that unrelated world or overwrite the recoverable map snapshot.

This is an intentional exception with a feature reason: prevent an intervening mission from making an old map plan eligible for automatic recovery. Retain only as documented cleanup until an equivalent lifecycle owner is established. Blindly adding a Lobby-only guard loses that protection. Test Lobby A, then an unrelated mission, then Lobby A, alongside an ordinary same-mission restart.

## Decisions by family

These are source-based dispositions, not blanket runtime approval.

| Family | Ownership and inherited behavior | Strict disposition |
| --- | --- | --- |
| Native AI groups | Active mode and playable/registered group; otherwise super. Editor preview separately requires authored Lobby mode. | Existing worlds use native groups and serialized GC attributes. An opt-in subclass migration must preserve those resources and spawn/empty-group behavior. |
| Group manager | Active manager suppresses automatic faction grouping; registered empty groups temporarily disable deletion around super. | Preserve native membership, frequency allocation and registration ownership. No shadow registry. |
| Callsign allocator/display | Authored selection only under Lobby authority; native allocation and automatic fallback retained. | Three added data methods read protected native allocation maps. They are accessors, not callback overrides; replacing them with another allocator is not simpler. |
| Pre-match phase setter | Active mode identity, master authority, SLOTTING/BRIEFING only. | Native `m_eGameState` is private (`SCR_BaseGameMode.c:205–206`); inspected native writes enter GAME/POSTGAME. Moving this method into a subclass does not solve private access. |
| Character/turret controls and flare animation | Active mode and relevant controller restriction; inherited control preparation preserved, other paths delegate. | Separate native control paths need coverage. Do not remove one until a single owner demonstrably enforces all restrictions. |
| Camera samples and spectator PIP | Active Lobby controller; PIP suppression also requires this sight to belong to the current spectator camera. | Protected optic state/direct camera timing explain the integration. Visual restoration and camera transitions still require tests. |
| Spawned gunner seat policy | Captured mode identity must remain active; otherwise clears and delegates. | Narrow per-gunner opt-in. No equivalent native policy established here. |
| Voice | Active manager/controller; inherited capture/receive callbacks preserved; owned capture has release path. | Require real transmission, reception, routing and mode-transition evidence before replacement or sign-off. |
| Map marker permissions | Phase gates UI and server mutation separately; mission/server markers remain distinct from player insertion. | UI-only enforcement cannot replace the server RPC check. Native permission alternative is unproven. |
| Map persistence/appearance | Active plan owner for persistence; active Lobby presentation for styling; native callbacks preserved. | Keep recovery retention separate from presentation. Source-visible restoration is not visual proof. |
| Briefing scroll | Active mode/controller and pointer inside Lobby briefing. | Native action listeners are separate from widget event consumption; a widget-only replacement needs verification. |
| Chat and lottery | Active manager plus command/menu/channel context; inherited behavior otherwise. | Prefer owned panels/menus where wiring permits. Late HUD registration has a concrete ordering purpose. |
| Inventory catalogue | Explicit gear session or opted-in Workbench browser; listeners, panel and drag state released on close/session loss. | Investigate opt-in menu subclass/resource before adding more hooks. Do not copy the native inventory implementation. |
| Admin crate capacity | Replicated opt-in, active manager and suitable top-level crate; manager resets owned flags on shutdown. | Dedicated crate storage subclass is a candidate only after all creation/resource paths are verified. |
| Admin physics freeze | Explicit hold, captured mode identity, conditional restoration of owned simulation state, callback cleanup on exit/delete. | Cleanup must work after mode exit. An unconditional early return outside Lobby would strand state. |
| Boundary teleport | Explicit server/GAME/body checks; receiving RPC rechecks mode/body and preserves character view. | Added ownership-routed method, not a native callback override. Local render transforms are not an equivalent replacement. |
| Native controller membership | Active server manager; changes native IDs only when needed. | Added method reaching native fields/RPCs; no parallel membership. |
| Workbench animation readiness | WORKBENCH build plus active item-browser ownership; super otherwise. | Explicit authoring opt-in, not permission to alter ordinary gameplay inventory. |

## Standard for retained hooks

1. Record which native API/event or opt-in subclass was considered and the concrete gap.
2. Check active mode and specific feature/entity/session before custom behavior; preserve inherited return values outside ownership.
3. Recheck delayed/RPC execution and compare mode identity where state can survive a transition.
4. Remove listeners and restore only state acquired by the feature. Cleanup remains possible after mode exit.
5. Verify active behavior, unrelated-mode behavior, transitions and an addon-free baseline. Authority and voice hooks require their relevant client/server evidence.
6. Keep global overrides thin. Do not replace small hooks with a larger polling/registry subsystem without demonstrated value.

Project AGENTS now records this standard. Gameplay code remains unchanged; the migrations and isolation tests above are still required.

## Evidence and limits

Installed catalogue revision: `c52648a62fcb4229ad089610d060ea44`, game/Workbench 1.9.0.37. Native paths above are under `game/data/data007.pak/scripts/`. Inspected script bodies establish inventory invokers, both muzzle contracts, callsign data and private game-mode state. Generated storage declarations establish the cached contract, not all C++ behavior.

Scans covered production `scripts/Game` and `scripts/GameCode`. This does not certify external addons, all resource inheritance, multiplayer or binary engine behavior. No new compile/play run is claimed for this source review. Unrelated working-tree edits were not changed.

## Complete shared declaration inventory

Paths are relative to the production Lobby2 addon. Repeated native types indicate separate declarations, not extra component instances.

| Native type | Declaration |
| --- | --- |
| `SCR_InventoryStorageManagerComponent` | `scripts/Game/Admin/GC_AdminGearAudit.c:55` |
| `SCR_UniversalInventoryStorageComponent` | `scripts/Game/Admin/GC_AdminGearStorage.c:2` |
| `SCR_EditableEntityComponent` | `scripts/Game/AO/GC_BoundaryMovement.c:2` |
| `SCR_ChatPanelManager` | `scripts/Game/Chat/GC_LotteryChatCommand.c:2` |
| `SCR_InventorySlotGearInspectionUI` | `scripts/Game/Loadout/GC_CatalogueInspectionSlot.c:2` |
| `SCR_InventoryStorageManagerComponent` | `scripts/Game/Loadout/GC_CatalogueInventoryManager.c:3` |
| `SCR_CharacterInventoryStorageComponent` | `scripts/Game/Loadout/GC_ForcedClothingStorage.c:3` |
| `SCR_InventoryStoragesListUI` | `scripts/Game/Loadout/GC_InventoryCatalogueHooks.c:2` |
| `SCR_InventorySlotWeaponSlotsUI` | `scripts/Game/Loadout/GC_InventoryCatalogueHooks.c:17` |
| `SCR_InventoryMenuUI` | `scripts/Game/Loadout/GC_InventoryCatalogueHooks.c:26` |
| `SCR_CharacterCameraHandlerComponent` | `scripts/Game/modded/Character/GC_CharacterCameraSample.c:1` |
| `SCR_AICombatComponent` | `scripts/Game/modded/Character/GC_SpawnedGunnerCombat.c:2` |
| `SCR_EditableEntityComponent` | `scripts/Game/modded/Entities/GC_AdminFreezePhysics.c:2` |
| `SCR_TurretControllerComponent` | `scripts/Game/modded/Entities/GC_LobbyTurretController.c:1` |
| `SCR_AIGroup` | `scripts/Game/modded/Entities/SCR_AIGroup_M.c:6` |
| `SCR_CharacterControllerComponent` | `scripts/Game/modded/Entities/SCR_CharacterControllerComponent_M.c:3` |
| `SCR_BaseGameMode` | `scripts/Game/modded/GameMode/SCR_BaseGameMode_M.c:4` |
| `SCR_CallsignGroupComponent` | `scripts/Game/modded/Groups/SCR_CallsignGroupComponent_M.c:2` |
| `SCR_CallsignManagerComponent` | `scripts/Game/modded/Groups/SCR_CallsignManagerComponent_M.c:2` |
| `SCR_FactionCallsignData` | `scripts/Game/modded/Groups/SCR_CallsignManagerComponent_M.c:24` |
| `SCR_CallsignCompanyData` | `scripts/Game/modded/Groups/SCR_CallsignManagerComponent_M.c:37` |
| `SCR_CallsignPlatoonData` | `scripts/Game/modded/Groups/SCR_CallsignManagerComponent_M.c:46` |
| `SCR_GroupsManagerComponent` | `scripts/Game/modded/Groups/SCR_GroupsManagerComponent_M.c:3` |
| `SCR_MapMarkerWidgetComponent` | `scripts/Game/modded/Map/GC_MapMarkerWidgetComponent.c:1` |
| `SCR_MapMarkerManagerComponent` | `scripts/Game/modded/Map/SCR_MapMarkerManagerComponent_M.c:1` |
| `SCR_MapMarkerSyncComponent` | `scripts/Game/modded/Map/SCR_MapMarkerSyncComponent_M.c:1` |
| `SCR_MapCursorModule` | `scripts/Game/modded/UI/GC_MapCursorModule_Briefing.c:3` |
| `SCR_MapMarkersUI` | `scripts/Game/modded/UI/SCR_MapMarkersUI_M.c:1` |
| `SCR_MapRadialUI` | `scripts/Game/modded/UI/SCR_MapRadialUI_M.c:1` |
| `SCR_2DPIPSightsComponent` | `scripts/Game/modded/Weapon/SCR_2DPIPSightsComponent_M.c:1` |
| `SCR_PlayerControllerGroupComponent` | `scripts/Game/Player/GC_LobbyNativeMembership.c:3` |
| `SCR_FlareAnimationComponent` | `scripts/Game/Setup/GC_SetupFlareAnimation.c:2` |
| `SCR_MuzzleEffectComponent` | `scripts/Game/Stats/GC_AIWeaponEvents.c:3` |
| `SCR_OptimizedMuzzleEffectComponent` | `scripts/Game/Stats/GC_AIWeaponEvents.c:14` |
| `SCR_ChatPanel` | `scripts/Game/UI/GC_LobbyChatPanel.c:2` |
| `SCR_ChatHud` | `scripts/Game/UI/GC_LobbyMenuChat.c:97` |
| `SCR_ChatPanelManager` | `scripts/Game/UI/GC_LobbyMenuChat.c:115` |
| `SCR_VoNComponent` | `scripts/GameCode/Network/SCR_VoNComponent_M.c:1` |
| `SCR_VONController` | `scripts/GameCode/Network/SCR_VoNController_M.c:1` |

## Complete Lobby2-owned declaration inventory

These extend Lobby2-owned implementations, not global base-game classes. Their lifecycle and RPC ownership still need review.

| Lobby2 type | Declaration |
| --- | --- |
| `GC_LobbyManager` | `scripts/Game/Admin/GC_AdminControlFreeze.c:1` |
| `GC_LobbyManager` | `scripts/Game/Admin/GC_AdminGearStorage.c:43` |
| `GC_LobbyManager` | `scripts/Game/Admin/GC_AdminPlayerTools.c:10` |
| `GC_LobbyManager` | `scripts/Game/Admin/GC_ForcedClothing.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/AO/GC_AOWarning.c:122` |
| `GC_LobbyGameMode` | `scripts/Game/GameMode/GC_JipGameMode.c:1` |
| `GC_LobbyManager` | `scripts/Game/Lobby/GC_LobbyManager_Voting.c:1` |
| `GC_LobbyManager` | `scripts/Game/Lobby/GC_LobbyMissionOperations.c:2` |
| `GC_LobbyManager` | `scripts/Game/MissionFramework/Runtime/GC_FrameworkModeActions.c:2` |
| `GC_LobbyManager` | `scripts/Game/Parameters/GC_LobbyParameterRuntime.c:3` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_AdminControlFreezeController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_AdminGearController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_AdminMissionController.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_AdminPlayerToolsController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_BoundaryAdminController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_BoundaryDetectionController.c:8` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_ForcedClothingController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_JipController.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_JipVehicleController.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_EventLog.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_Logs.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_MapBriefing.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_MapPlans.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_MapShapes.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_ReportPages.c:21` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_Stats.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_Status.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_LobbyPlayerController_Voting.c:1` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_SpectatorBodyFollowController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_SpectatorFocusController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_SpectatorPlayerJumpController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Player/GC_SpectatorRadioController.c:2` |
| `GC_LobbyPlayerController` | `scripts/Game/Setup/GC_SetupPlayerController.c:2` |
| `GC_SessionLogComponent` | `scripts/Game/Stats/GC_DebriefCombat.c:2` |
| `GC_MissionStatsComponent` | `scripts/Game/Stats/GC_DebriefService.c:2` |
| `GC_LobbyManager` | `scripts/Game/Stats/GC_LobbyManager_PlayerHistory.c:2` |
