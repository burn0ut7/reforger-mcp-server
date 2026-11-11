# Native reuse review — 2026-11-11

The objective is less custom behavior to maintain without losing features. Prefer an engine owner or primitive when it provides the required contract. Retain custom code for an identified gap, and distinguish an unverified replacement from a demonstrated equivalent. This is a targeted helper/ownership audit, not certification of every framework path.

## Decisions

| Area | Finding | Decision |
| --- | --- | --- |
| Ban expiry (`GC_AdminBanExpiry`) | Manual Gregorian calendar arithmetic duplicated the native wall-clock timestamp conversion. Native parsing needs strict round-trip validation because failure returns zero and date normalization must not broaden accepted inputs. | Replaced arithmetic with `Timestamp.FromIsoString`, `ToIsoString`, `ToEpochSeconds`; retained format, UTC and 1970–2037 policy. Native `IsDigitAt` replaces substring scanning. 87 live checks passed. |
| Callsigns (`GC_GroupCallsignHelper`) | Already delegates names and localization to the game; remaining code handles project presentation/manual values. | Retain the thin policy layer. A helper name alone does not establish duplication. |
| Platform identity (`GC_PlayerPlatformIdentity`) | Obtains server-observed identity from the game and rejects disconnected, wrong-platform or malformed identities. | Retain authority and validation. Do not replace it with client-provided IDs. Native `IsDigitAt` is a possible small follow-up; differing length policies elsewhere must remain distinct. |
| Formation catalog (`GC_PlayerFormationCatalog`) | Reads resource formations with duplicate/missing-name validation and bounds-checked offsets. Native `AIFormationDefinition` exposes offsets and names, but no offset-count method in inspected declarations; live movement access is not equivalent to resource-independent authoring. | Retain pending a complete native resource contract. Do not swap in live group state merely to reduce lines. |
| Spawn anchor (`GC_PlayerGroupSpawnTransform`) | Stores authored placement because group transforms can recenter as agents are added. The snapshot also contains small vector-composition methods; their arithmetic can be reviewed separately from snapshot ownership. | Retain the authored snapshot; live position and authored anchor serve different purposes. |
| Spawn polygon (`GC_SpawnPolygon`) | Containment already uses `Math2D.IsPointInPolygonXZ`. Topology validation and tolerance rules are additional behavior. | Retain validation. Native `vector.DistanceSqXZ` could replace the small distance implementation while preserving its public method; low reduction compared with broader ownership work. |
| Inverted map fill (`GC_MapShapeOutsideFill`) | Ordinary `PolygonDrawCommand` has vertices but no hole representation in inspected declarations. `TriMeshDrawCommand` accepts explicit indices. | Retain triangulation for the outside-fill feature; ordinary polygon rendering is already native. |
| Admin text wrapping (`GC_AdminTextWrapping`) | The game supports wrapping, while custom code also splits long tokens on UTF-8 boundaries and preserves line behavior. | Unresolved candidate: measure native wrapping on long tokens, explicit newlines, Unicode and viewport sizes before replacing. No equivalence or live visual verification claimed here. |
| Prefab editing (`GC_LoadoutPrefab`) | Traversal resolves inherited objects and editor mutation paths, not just values. | No proven replacement yet. A direct container lookup alone does not establish equivalent inheritance-aware editing. |

## Higher-value follow-up candidates

- `GC_PlayerHistory.c:31–64` enumerates all players and resolves identity/statistics every second. `GC_LobbyManager_PlayerHistory.c:19–33` suppresses unchanged publication, but not the upstream scan. Registration/disconnection callbacks already exist in the manager. Investigate event-driven membership plus explicit refreshes for asynchronous history responses, staleness and playtime thresholds; replacing the loop with join events alone would lose behavior. No performance measurement or complete replacement established yet.
- `GC_BoundaryDetectionController.c:112–168` samples local movement every 100 ms while relevant polygons are active. It already suppresses unnecessary sends and uses native containment/proximity primitives; its server path uses inventory/compartment invokers. Determine whether native spatial events can cover arbitrary polygons and proximity bands before replacing movement sampling. No equivalent engine event established yet.
- `GC_SpectatorMarkerOverlay.c:953–995` already seeds from the engine editable-entity registry, subscribes to register/unregister events and releases subscriptions on destruction. Preserve that native ownership pattern rather than introducing another global registry.

## Evidence and limits

Installed Workbench/game version: 1.9.0.37. Catalogue revision: `c52648a62fcb4229ad089610d060ea44`. Evidence is installed source/declarations, not an assertion that every native implementation has been inspected.

- `game/core/data.pak/scripts/Core/generated/System/Time/Timestamp.c`, lines 25–43: epoch/ISO conversions and UTC/local views.
- `game/core/data.pak/scripts/Core/generated/Types/string.c`, lines 397–402: ASCII digit predicate.
- `game/core/data.pak/scripts/Core/generated/Types/vector.c`, lines 107–108: squared distance primitives.
- `game/core/data.pak/scripts/Core/generated/Math/Math2D.c`, lines 21–35: containment and triangle winding.
- `game/core/data.pak/scripts/Core/proto/EnWidgets.c`, lines 111–126: polygon and indexed mesh drawing contracts.
- `game/data/data007.pak/scripts/Game/generated/AI/AIFormationDefinition.c`, lines 1–24: formation offset/name API.
- `game/core/data.pak/scripts/Core/generated/UI/TextWidget.c`, lines 26–44: line count and wrapping controls. These declarations alone do not establish equivalence to custom long-token wrapping.

The inspected `game/data/data007.pak/scripts/Game/Helpers/SCR_DateTimeHelper.c:30–55` (`GetDayNumber`) implementation adjusts February for leap years only when month > 2, then checks the selected month's maximum day. That makes it unsuitable as a direct leap-February validator. Prefer native code after checking its actual contract, not merely because it is native.

Parser validation and reproducible fixture are in [NativePrimitives](tests/NativePrimitives/README.md). Multiplayer ban authorization was not changed or re-tested in this slice; the tests call parsers without issuing moderation actions. Existing unrelated ArlandLobby resource GUID errors remain. Other authors' dirty geometry/world/editor changes were not modified.

Further high-value review should focus on repeated world/player sampling, duplicated state ownership, event lifecycle, and removing complete redundant subsystems. Replacing isolated arithmetic wrappers offers limited line reduction; no substantial framework-wide reduction percentage is established by this pass.
