# Mission metadata

## Author workflow

Start Workbench with `-scriptAuthorizeAll` when using this workflow with trusted addons. This allows Workbench scripts to write files for that session. On the tested installation, a normal launch could save the world but refused the header write.

In World Editor, select your saved mission subscene and choose **Plugins > GC > Create Mission Data**. Fill in Name, Author, Description and Player Count. Minimum Players is directly below Player Count. Mission Type starts at Testing. Choose Objectives and Force Composition. Mechanized covers infantry operating with APCs or IFVs. Airborne covers parachute troops, helicopter operations and other crewed aviation. Drones and other support can be selected alongside any force type. The website derives Mods from non-vanilla addons referenced by the placed prefabs and their ancestors.

Press **Create mission data** once. Workbench saves the world, collects its slot and asset metadata, creates and registers the standard `Configs/Missions/<Author>/<Name>.conf` in the mission addon (spaces and underscores are removed from the entered mission name), and writes `<world>.ent.metadata.json` beside the world. Cancel exits before saving.

For an existing header, choose **Refresh metadata only** to save the world and refresh its metadata. Edit and save existing headers in Resource Manager.

Editing, adding or resetting catalogue properties never requests a scan. Opening worlds and registering resources no longer request scans either. The legacy scanner component remains only to preserve existing prefab serialization; it does no work.

Ordinary Ctrl+S saves normally but does not refresh metadata: the installed Workbench scripting API exposes no general save callback. Use the combined action when publishing changed mission content. There is no timer, filesystem polling or global entity override.

The collector visits only the selected mission subscene and its prefab children, excluding inherited terrain scenery and other subscenes. It refuses terrain scenes and Play/prefab modes. Progress is shown during collection. Guards abort collection at 100,000 entities, 128 nesting/inheritance levels or 30 seconds; failed collection does not replace the previous snapshot. These are cooperative guards between engine calls, not a guarantee against a stalled native call. The action is synchronous; large mission performance still needs measurement.

The generated file contains content evidence, not authored objective tags or a complete dependency list. It does not edit the mission header. Include it with the published mission files if the website should use it. Packed-addon inclusion has not been verified.


## JSON contract

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Currently `1`. |
| `world`, `scope`, `limitations` | Snapshot identity and explicit coverage limits. `scope` is `world_subscene`: the save action uses the selected mission subscene. Earlier exports used `active_subscene`. |
| `entityCount` | Entities visited, including nested helper entities. Not a unit count. |
| `factions` | Unique nonempty placed affiliation, playable-group and authored loadout faction keys. No friendly/enemy inference. |
| `playerSlots` | One row per authored playable-group slot: group name, zero-based index, group faction, character prefab, loadout name and loadout faction. AI-only groups are excluded. Random variants, runtime changes and inventory contents are not expanded. |
| `configuredFactions` | Faction keys configured in faction managers in the scanned subscene, reported separately from actual affiliations. |
| `assets` | Counts by detected category. Mounted vehicle subentities are not counted as independent vehicles/weapons. |
| `prefabs` | Inventory grouped by prefab, entity class, effective faction, asset and classification evidence. Each row includes `count` and `sourceAddonIds`. Entities without prefab ancestry have an empty prefab reference. |
| `referencedAddonIds` | Unique addons defining/modifying placed prefabs or their prefab ancestors, as reported by the engine. Includes vanilla content. |
| `loadedAddons` | Loaded addon IDs, GUIDs, titles, vanilla flag and `referencedByPlacedPrefab`. Merely being loaded does not imply placed content use. |

The website can consume this JSON directly. Rows and summaries are sorted for stable diffs. Existing mission-header metadata is not changed.

## Asset recognition

The exporter reads resolved sources, so ordinary prefab inheritance and instance faction/type overrides apply. It does not classify by filename substrings.

- Infantry: `ChimeraCharacter` inheritance.
- Helicopters: `VehicleHelicopterSimulation`, or the native AI usage type.
- APCs, cars, trucks, mortars and support vehicles: native `EVehicleType` values.
- Airplanes, static weapons/artillery and otherwise unclassified tracked vehicles: native AI usage type.
- Boats: verified RHIB base ancestry (`{A295678AF2AE1205}Prefabs/Vehicles/Naval/RHIB/RHIB_Base.et`), or an authored `VEHICLE_BOAT` editor label when supplied by a loaded mod.
- Tanks: an authored `VEHICLE_TANK` editor label when supplied by a loaded mod. Tracked movement alone does not imply a tank.
- Other vehicles remain `vehicle`. An empty asset category means the row is not classified as a gameplay asset.

`assetEvidence` records the rule used. Mod authors can configure incorrect types/labels; these categories describe authored configuration, not independently verified capabilities. Boat/tank enum names are resolved dynamically, so the plugin does not require those mods. The RHIB rule is an exact ancestry mapping; buoyancy alone would incorrectly classify amphibious APCs as boats.

## Deliberate limits

This is placed-content metadata, not a complete mod dependency analyzer. Script-only mods, components' resource references, loadout inventory, random variants, catalogs and runtime spawners are not recursively expanded. Playable-group slot prefab references are included. The loaded-addon list preserves context without claiming those addons were all used by placed prefabs. No era, difficulty, mission purpose or player availability is guessed. No positions or mission-header fields are exported.
