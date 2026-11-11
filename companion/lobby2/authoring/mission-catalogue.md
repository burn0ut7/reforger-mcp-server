# Mission catalogue metadata

Use `GC_MissionHeader` for a mission's scenario header. Standard name, author,
description, details, world and player-count fields remain owned by the game.
`m_GC_Catalog` adds author-confirmed information for the GC website. This schema
does not change player slots, factions, mission logic or game-mode behavior.

`Configs/Missions/ArlandLobby.conf` is a minimal unlisted authoring example used
by the website's local Arland test entry. It deliberately leaves capacity,
objectives and equipment unspecified. Register a scenario through Workbench
when publishing a launchable mission, preserving its generated `.conf.meta`.

| Native field | Website field |
| --- | --- |
| `m_sMissionType` | `missionType`: TVT, COOP, COTVT, LOL, OTHER, SD, AAS, TRNG, TEST |
| `m_sMapName` | `map`: display name, not world identity |
| `m_aObjectives` | `objectives`: multiple authored labels |
| `m_iMinPlayers`, `m_iRecommendedPlayers` | `minPlayers`, `recommendedPlayers` |
| Header `m_iPlayerCount` | Maximum capacity when explicitly authored |
| `m_sEra` | `era` |
| `m_aPlayerAssets` | `assetTypes`: player-available equipment |
| `m_aMods`, `m_aRequiredAddonIds` | Display names and declared required addon GUIDs |
| `m_aForces` | Faction key, force type, side label, playable status and intended slots |
| `m_sGroupId`, `m_fGroupSimilarity` | Mission family and optional similarity |
| Header `m_aManualTags` | Additional authored tags |

Optional native counts use zero for unspecified. Playable status is -1 unknown,
0 AI only, 1 playable. Similarity is -1 unknown or 0–1. Do not label an opposing
force playable just because it is an opposition faction. Objective suggestions:
Defense, Attack & Defend, Assault, Convoy, HVT, Sweep, Hostage, Delay, Search & Rescue.
The editor offers objective and equipment dropdowns. Use Manual Tags for additional custom labels. Existing serialized custom labels remain supported by the website. Maximum tag length is 80 characters.

The website imports these native fields and the older GC `m_GC_Metadata` fields.
An optional `<scenario>.conf.metadata.json` overrides individual author fields;
omitted properties preserve header values. Keep one authoring source whenever
possible instead of maintaining duplicate metadata. Publish the saved `.conf`
and `.conf.meta` to the configured mission repository. The website worker checks
on startup and every ten minutes when enabled; no manual website import is needed.

The existing Workbench scanner writes **generated content evidence** beside the
world as `<world>.ent.metadata.json`. The website now discovers that file through
the scenario's world path in the same repository. Save/export and commit it with
the mission if desired. It is a partial editor snapshot, not a complete dependency
or player-equipment inventory. Use **GC > Create Mission Data > Refresh metadata only** to refresh it. **Submit Mission** also refreshes the snapshot before review.
The picker labels the authored local starting hour as Morning (06–11), Midday (12–16), Evening (17–20), or Night (21–05). Starts spanning periods show Variable. A missing or ambiguous source leaves the label empty.
Ordinary saves and property edits do not trigger scans. Save the header separately
in Resource Manager. No production game/server scans are added.

Runtime COs, teams, results, statistics and playback remain session records;
TilW's executable conditions are not automatically turned into public objective
tags. Required mods are author declarations, not inferred from all loaded addons.
