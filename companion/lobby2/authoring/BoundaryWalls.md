# Setup and AO boundary walls

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
