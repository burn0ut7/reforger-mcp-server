# Setup and AO boundary walls

## Selected map-area previews in Workbench

Selecting an entity with `GC_MapShapeComponent` (including `MapShape.et` and
`AOLimit.et`) shows its configured map outline and translucent fill in the 3D
viewport. Outline color, faction color, fill color/opacity and
inverted fill use the map settings. Selection previews deliberately ignore the
runtime Enabled and faction-audience filters so authors can edit hidden areas.
Open polygons show their outline only until closed.
Closed polygons require 3–128 points, nonzero horizontal area, and no crossed,
touching or overlapping non-adjacent edges. Invalid shapes show an outline and
log a warning instead of a misleading fill. Maps and AO limits use the same
validation; a rejected runtime polygon replacement preserves the previous shape.

Only selected entities receive these previews; multi-selection shows each
selected area's style. Deselecting removes its preview. Radius Area's existing
sphere helper is also selection-only. Workbench's own spline handles and native
editing lines remain separate from these map previews.

The 3D translation is a flat X/Z projection at the shape origin's height, drawn
through terrain. Outline widths use 0.1 metres per authored map pixel (3 pixels
becomes 0.3 metres); they are world-space ribbons rather than screen-space lines.
Inverted fills extend to the world bounds instead of a 2D map
viewport. Multiple translucent fills blend where they overlap. Geometry is
cached until points, transforms or style change. Geometry rebuilds wait until
points remain stationary for 250 ms, so dragging does not continually rebuild
meshes. The previous preview remains visible until the new position settles.
Geometry edits replace meshes while reusing entities and materials; spare
batches are released when fewer are needed. Color changes replace the materials.
The outside-fill canvas captures world bounds when selected, preventing temporary
preview geometry from feeding back into those bounds. Nothing is saved
into the world, and the previews run only in Workbench edit mode, never Play or
the actual game. This does not change AO enforcement or runtime boundary walls.

## Runtime boundary walls

`SetupZone.et` includes an enabled `GC_BoundaryWallComponent`.
`AOLimit.et` includes the same component with **Enabled** off by default.
Change its **Boundary wall** settings on the placed prefab:

- **Enabled**: display the wall for players affected by this zone.
- **Range**: horizontal radius around the player in which wall portions appear; default 10 m.
- **Height**: height above terrain; default 3 m.
- **Color**: RGB tint and maximum opacity; default translucent red, alpha .35.

The wall follows the enforcement polygon, shows only nearby portions with a
smooth fade toward the range limit, and has no collision. Approaching one edge
does not illuminate the rest of the perimeter. It appears on both sides so a player
outside a soft boundary can find the way back. Setup release removes it. AO uses
its existing faction, item and vehicle eligibility, also during setup. It is
suspended while the player is under admin freeze.

At 10 m or farther the default wall is invisible. Proximity checks are capped at
about 30 per second. The entity and material are reused. After at least 10 cm of
horizontal movement, only clipped nearby strips are rebuilt; stationary players
and camera turns do not rebuild geometry. The server publishes eligibility/polygon
changes, not per-frame visual updates.

The server replicates eligible character identities and current canonical polygon;
each client creates its own native mesh with the vanilla VirtualArea texture. Color changes do
not alter shared vanilla materials. No separate helper prefab is required.

The component supports **3–60 polygon vertices**. More vertices produce
an explicit log error and no wall; enforcement still runs. Terrain height is
sampled at polygon vertices, so place vertices at important slope changes.
Runtime mission scripts may call the component's server-only
`Configure(enabled, range, height, packedArgbColor)`; accepted ranges are 1–100 m
and .5–20 m. This changes appearance only.

See `Art/References/boundary-wall/validation.md` for captures and coverage limits.
Local surface fading is covered in `Art/References/boundary-wall/local-fade/validation.md`.
