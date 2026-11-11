# Briefing marker component

`GC_BriefingMarkerComponent` is placed on a named world entity. The owning
entity's name is the stable key used by briefing `<marker name="...">` links and
replicated runtime updates; the entity's world origin and yaw become the marker
position and rotation. Entity names used by markers must be non-empty and
unique. There is no separate marker-name property on the component.

Place `Prefabs/Logic/Mission/MapMarker.et` and give the entity a unique name.
It starts with a white circle icon at 48 screen pixels. Configure its
`GC_BriefingMarkerComponent` to choose the image, label, color and audience.

Workbench renders a direction arrow in the authored marker color. It follows
position and yaw; pitch, roll and entity scale do not affect map markers.
Moving a marker in Workbench snaps its authored Y position to terrain height at
the new X/Z position, as part of the editor transform operation.
Selecting a marker also shows its actual icon on a flat horizontal plane, fifteen
centimetres above the entity. The preview follows the terrain-snapped entity;
it does not sample terrain or rebuild geometry every frame. Depth testing is
disabled, matching the game's editor direction indicator, so terrain and other
world geometry do not hide the selected preview. Sorting alone does not disable
depth testing.
The preview uses the map's yaw-minus-90 rotation and
preserves the icon's aspect ratio. World-scaled icons use the authored width in
metres; screen-scaled icons use a five-metre preview width. Minimum pixel size,
labels, hover highlights and faction/phase visibility are map-only presentation.
The preview disappears on deselection and is not saved or replicated. All preview
code is compiled only for Workbench, runs only in edit mode, and is cleared for
embedded Play; no 3D marker is created in an actual game session.

## Authored properties

- **Image** accepts either a texture or an `.imageset` resource.
- **Quad Name** is required only for `.imageset` resources and is ignored for
  standalone textures.
- **World Scale** off treats **Size** as screen pixels. On treats **Size** as
  world metres, so the marker grows and shrinks with map zoom.
- **Color** tints both the map marker image and the Workbench direction arrow.
- **Description** is displayed in black beneath the image and scales with it.
  World-scale descriptions grow and shrink with map zoom; screen-scale descriptions
  keep their screen size. A 48-pixel icon uses 18-pixel text, with the label area
  scaled proportionally. The font rounds to whole pixels with a one-pixel minimum.
- **Faction Keys** is an array of authoritative base-game faction keys. An
  empty array shows the marker to everyone.
- **Show At Game Mode State** selects the state in which the marker is shown.
- **Invert Game Mode State** off shows only in the selected state; on hides in
  the selected state and shows in all other states.

Runtime visibility is deliberately not authored. Server scripts can call
`GC_LobbyManager.SetBriefingMarkerVisible(markerName, visible)` without changing
the authored rules.

## Briefing link focus

`<marker name="EntityName">label</marker>` resolves only against the replicated
markers visible to the current player. Clicking the label first pans to the
marker at the current zoom. After the map's interpolated-pan completion event,
it zooms while keeping the marker in the map area not covered by the open
briefing panel.

Every click recalculates marker zoom from its authored size and current
projected footprint. The target accounts for rotation, the uncovered map
viewport, and the map's minimum and maximum zoom. The rotated marker is framed
to occupy roughly one third of the available map's shorter dimension, and a
description is vertically centered with its marker. Screen-scale markers use
their authored size as a virtual world footprint for focus framing while their
rendered pixel size remains independent of map scale. Missing, hidden, or
unavailable markers produce a
`GC | Briefing marker focus failed: ...` warning in the console.

## Image-set quad names

Enter an exact image-set quad name, for example `circle-2` or `arrow-large` in
`icons_mapMarkersUI.imageset`. The selected 3D preview updates when the image or
quad changes. Invalid resources or quads leave the direction arrow visible and
emit a preview warning instead of displaying a substitute icon. Scripts can
enumerate names with `WidgetManager.GetImageSetImages`; this component currently
uses a thumbnail resource picker and a separate text field.

## Multiplayer ownership

World components submit their initial snapshots only on the server.
`GC_LobbyManager` owns runtime changes, broadcasts reliable upserts/removals,
and includes the complete latest marker list in `RplSave`/`RplLoad` for JIP.
Clients derive faction and game-mode visibility locally from the replicated
marker payload and authoritative base-game state.

## Future import/export boundary

The replicated `GC_BriefingMarker` payload contains only serializable values:
name, resource paths, quad, position, rotation, scale mode, size, color, description,
faction keys, state rule, runtime visibility, and revision. No import/export or
disk I/O is implemented in this slice, but the payload does not retain entity or
widget references, so a future loader can construct the same records.
