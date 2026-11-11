# Map markers and vehicles

Both actions belong in an event's Actions list on GC Lobby Manager. Conditions and Delay decide when they execute. They run once per event execution on the authoritative server, with no frame checks, world scans or spawning hooks.

## Map Marker Visibility Action

- **Markers:** names of world entities with a **GC Briefing Marker** component. A marker and its displayed text change together; the entity is not deleted.
- **Factions:** add a faction key and a **Visible** checkbox for each faction to change. Checked means visible (true); unchecked means hidden (false). Unlisted factions retain their current setting.
- **Visible Without Faction:** whether players without an assigned faction can see the marker. This is always applied, including when Factions is empty. Faction-assigned spectators continue to use their assigned faction.

The existing marker's phase requirement and global visible state still apply. Hiding the last permitted faction hides the marker from all factions; it does not turn an empty audience into everybody. Missing markers are skipped. Invalid faction keys, duplicate faction entries or audience limits reject the action before publishing any marker changes. Up to 128 marker names and 16 faction entries are supported; a marker's resulting stored audience must also fit 16 keys.

Example: for `Objective_A`, add `US` with Visible checked and `USSR` unchecked. Leave Visible Without Faction unchecked. Other factions retain their previous visibility.

This supports per-faction and empty-faction marker visibility. It targets Lobby2 catalog markers, not legacy PS/COA markers, separate PS_MissionDescription entities, player-drawn markers or map shapes. Lobby2 does not integrate with TilW Mission Framework; automatic legacy mission conversion is outside this action. Marker color, position, text, phase and size remain intact.

Implementation: detached catalog snapshots are validated before upsert. Existing catalog revision, reliable update and joining-player snapshot paths carry the audience. Three audience booleans were added consistently to marker copying, stream serialization and snapshot serialization/comparison. Server and clients must use the same updated Lobby2 build. The existing SetBriefingMarkerVisible method now copies the full marker before toggling visibility, preserving the audience. SetBriefingMarker continues to replace the complete definition and its audience.

## Vehicle Fuel, Supplies and Repair Action

- **Entities:** exact names of existing vehicles or other entities with the relevant components.
- **Change Fuel:** set **Fuel Ratio**, using a 0–1 slider: 0 empty, 0.5 half full, 1 full. Includes attached fuel managers and storage tanks, matching TilW's unfiltered fuel operation.
- **Change Supplies:** set **Supplies Amount** as an absolute total. Zero empties the supplies; the engine clamps values to storage capacity. This does not refill weapon magazines.
- **Repair:** fully repair supported damage through the engine damage manager, including vehicle hit zones and drowned engines. This does not spawn replacements for deleted entities or missing parts.

Each checkbox is independent. At least one operation and one named target are required. Unchecked numeric settings are ignored. Supply amounts must be 0–1000000; fuel must be 0–1. Missing entities and unsupported components are skipped, allowing mixed targets. Duplicate entity targets are handled once. If no requested component is found at all, the framework reports a rejected action. A supported request already at its desired value is accepted.

This covers all three options in TilW's Service Vehicles action: fuel, carried supplies and full repair. Existing entities, occupants and unrelated cargo are retained. No rearm-magazine feature existed in that TilW action, so none is implied here.
