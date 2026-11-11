# Standalone area prefab spawner

Place **Prefabs/Logic/Spawning/AreaPrefabSpawner.et** and edit its closed
polyline. The supplied shape is a 20 × 20 metre square. Add entries under
**GC_AreaPrefabSpawnerComponent > Prefabs**, choosing a prefab and count for
each. Save a child prefab to reuse the shape and settings.

This is an opt-in component on a standard `PolylineShapeEntity`. It has no
dependency on Lobby2's game mode, flags, capture or mission-rule engine. Loading
the addon does not run it; a placed/spawned configured instance does. All
creation is server-only and nothing spawns in edit mode.

## Placement settings

- **Spawn On Init:** run once after play initialization. Disable for explicit
  activation from a trusted server script.
- **Avoid Water:** reject water detected at the terrain sample. Finite river
  volumes above the bed can be missed; this is not a full water-column check.
  Off allows seabed
  placement; it does not place boats on the water surface.
- **Random Yaw:** random heading per object; off preserves the spawner's world
  heading. **Align To Terrain** aligns pitch/roll with the sampled surface.
- **Height Offset:** vertical offset applied after surface alignment.
- **Minimum Spacing:** minimum horizontal distance between origins created by
  this instance. Other world entities are not part of this spacing list.
- **Clearance Radius** on each entry: optional stationary physics box trace
  above the placement surface. The value is the horizontal half-width; the box
  height is twice that value. Only entities colliding with the projectile
  physics mask are checked, using one trace per candidate. No entity sphere
  enumeration is performed. Zero disables it. Set the radius to suit
  the prefab; this is not an exact bounding-box or mesh-overlap guarantee.
- **Attempts Per Object:** limits candidate checks, including rejected points
  in a concave polygon. Obstructed or unsuitable areas can produce fewer objects.

The polygon's world transform is respected, including rotation and translation.
Only XZ shape matters; objects are placed on the sampled terrain surface rather
than at the polyline vertices' height. Concave polygons are supported;
self-intersections, repeated adjacent points, open or zero-area shapes are
rejected. The **spawn origin** must be inside; large meshes can extend outside.

Limits: 3–128 vertices, 32 entries, 128 total objects, 1–256 attempts per object,
and at most 4096 candidates per instance. Resource/settings checks finish before
any payload is spawned. There are no temporary probe objects to measure bounds.
Counts are per entry; duplicate prefab entries intentionally add their counts.

## Triggering and mission events

For a mission event, use the existing **Spawn Action → Prefab** to create your
configured child of AreaPrefabSpawner with **Spawn On Init** enabled. Its saved
shape is transformed to the Spawn Action's named location. Each new spawner
instance makes one independently randomized batch. Use an empty output name or
unique names when creating repeated batches.

For an already-placed, manually activated instance, server script can call:

```c
IEntity area = GetGame().GetWorld().FindEntityByName("ReinforcementArea");
if (area)
{
	GC_AreaPrefabSpawnerComponent spawner = GC_AreaPrefabSpawnerComponent.Cast(area.FindComponent(GC_AreaPrefabSpawnerComponent));
	if (spawner)
	{
		int created = spawner.Spawn();
	}
}
```

This is a trusted script API, not a client RPC or admin command. No new mission
action is required. A mission framework may wrap this API in its own authorized
trigger; the spawner does not depend on that framework.

`Spawn()` is **once per instance**, including partial/failed attempts. Repeated
calls do not create duplicates or retry placements. It returns -1 for authority,
edit-mode or preflight rejection, otherwise the number of roots actually
created. Inspect `IsComplete()`, `GetSpawnedCount()`, `GetAttemptCount()` and
`GetLastError()` to distinguish success from partial placement. Do not treat a
dispatched call as proof that the desired count was achieved.

Successfully created roots remain ordinary world entities. Deleting the spawner
does not delete them. `GetSpawnedEntities(out array<IEntity>)` returns a snapshot
of those roots for caller-owned follow-up work; entries can become null if other
systems delete them. There is no automatic retry, periodic spawning, cleanup,
crew assignment, waypoint assignment or mission-state ownership here.

Payload prefabs retain their own replication and component behavior; the
spawner does not add replication to an unreplicated prefab. A partial result
retains already-created roots and reports why the batch did not finish. It is
not a transaction over arbitrary payload-component side effects.
