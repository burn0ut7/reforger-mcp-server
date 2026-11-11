# Respawn locations

**Set Respawn Location Action** defines the faction's default for all replacement-character respawns, including individual heal-and-respawn, admin group/player recovery, briefing body replacement and automatic/scripted waves. The shared respawn transaction resolves it immediately before creating a replacement. Initial deployment and reconnecting to an existing living character are not respawns.

**Respawn Wave Action > Location** optionally overrides that default for one action. Explicit original/current-position admin commands also remain deliberate overrides. Without a configured default or override, existing callers retain their normal original/current fallback.

| Location | Behavior |
| --- | --- |
| Original slot position | Explicitly use the slot's saved start, ignoring any faction default. |
| Current character position | Explicitly use the existing slot character/corpse's position and rotation. Reject if the body no longer exists. |
| Named entities | Select one or more uniquely named world entities. Every spawn reads the selected entity's current position and heading. Players cycle through the list, then fill successive rows at each destination. |
| World position | Use fixed world coordinates and a heading. |

Named and fixed locations provide a local **Offset**, **Spacing** (default 2 metres), **Columns** (default 4), and optional **Snap To Terrain**. The grid extends right and backward from the anchor. Offset uses right/up/forward coordinates relative to heading. Terrain snapping uses terrain height plus the offset's Y value; leave it off for bridges, roofs and other elevated surfaces. Characters remain upright even if the anchor tilts or scales.

Placement numbering restarts for each wave and is shared by players using the same location object. An attempted placement advances the index even if the anchor is missing or the spawn fails, so a missing first anchor does not prevent later destinations being tried. Players skipped for lack of tickets do not take a placement index. These are authored positions, not a collision, water, enemy-proximity or safe-ground search. Separate location objects may overlap; authors must provide enough clear space. No vehicle seating is implied.

## Changing destinations during a mission

Use **Set Respawn Location Action**, select explicit **Factions**, and add a **Location**. This changes their default for all subsequent respawns without an explicit location override. It does not move or respawn living players. Run another Set action to replace the default; an empty Location restores the callers' normal original/current fallback. Choose Original Slot Position explicitly if every ordinary recovery must return to the starting point. All faction names and configuration are validated before any default changes. Defaults belong to the running mission and disappear when that game mode is destroyed.

Named anchors are resolved at spawn time, not cached at mission start. An anchor can be authored in the world, spawned later, moved or rotated using normal mission scripts, or be a moving vehicle. Use a local offset to place players beside that vehicle. Deleting an anchor makes it unavailable until another entity with that exact name exists. An unavailable selected destination skips that player without consuming a ticket or falling back elsewhere. Duplicate names in the world are unsupported; give anchors unique names.

Briefing death/deletion recovery also uses the shared destination. If recovery cannot resolve the location, the claimed slot remains KIA rather than being silently recreated by initial-spawn catch-up. Restore a valid destination and use an explicit recovery command, or a later Match respawn. Current Character Position requires a retained slot body; briefing cleanup/deletion clears that reference, so that policy cannot recover it without an explicit different destination.

Examples:

- **Capture a forward base:** the capture event runs Set Respawn Location for the capturing faction, pointing at a named base marker. Existing automatic waves now use that base.
- **Mobile rally point:** use the name of a support vehicle, offset behind it, and terrain snapping. Every later wave follows its current location and heading.
- **One reinforcement insertion:** give a single Respawn Wave Action its own Location; later waves still use the faction default.
- **Multiple landing points:** list named markers in their desired order; players distribute across them, then into rows. The existing Random Order option randomizes players, not destination order.
- **Restore the original deployment:** set the faction default to Original Slot Position, or choose it explicitly for a single action. Clearing the default restores each caller's previous original/current fallback.

Existing faction filters, maximum-player limits, ticket refunds, connected/KIA eligibility and variant policies still apply. Admin commands explicitly requesting original/current positions retain those meanings and ignore mission defaults; ordinary Heal/Respawn commands use the default when replacing a dead player. Individual recovery uses placement index zero; wave batches allocate indices per location. A failed location resolution, spawn or possession retains the old slot/body and refunds any ticket reserved by a wave. Admin recovery still uses its existing permissions and ticket-free behavior.

## Mission-script extension

Trusted server scripts can call `GC_LobbyManager.GC_SetRespawnLocation(factionKeys, location)` and pass an optional fifth argument to `GC_RespawnWave(factionKeys, maximumPlayers, useTickets, randomOrder, location)`. These are server mission APIs, not client/admin RPCs. Set rejects invalid factions, inactive owners, shutdown, postgame and changes during an active wave. Wave retains its Match-only authority gate.

For custom rules such as a living squad leader, safe-position sampling, random areas or mission-specific eligibility, derive an attributed class from `GC_RespawnLocation`. Implement `IsValid()` and `Resolve(BaseWorld world, GC_Slot slot, int index, out EntitySpawnParams parameters)`. Resolve on the authority using the current slot/native group state; return false when unavailable. Return world-space spawn parameters for a custom location, or true with null parameters only to explicitly request the original spawn path. Keep resolution free of unrelated side effects and do not recursively invoke waves. `GC_PlacedRespawnLocation` supplies offset/grid/terrain behavior for subclasses implementing `GetAnchor`.

No runtime destination entities, polling service, copied group membership or additional addon dependency are required. The existing respawn transaction owns character creation, possession, slot updates and ticket refunds.
