# Workbench mission template

Open a mission derived from a supported terrain, select an unlocked layer in the
mission subscene, and choose **Plugins → GC → Spawn Mission Template**. Reforger
places native `WorkbenchPlugin` actions in Plugins; its Tools menu contains
interactive editor tools such as Map / AO Polygon.

The plugin places `Prefabs/Templates/GC_MissionTemplate_Coop.et` at the world origin and creates the
terrain's AI-world prefab as its child. The root inherits `GameMode_Lobby.et` and
contains the faction, radio, GC chat, and perception entities. Lobby2's mission
framework has no TilW Mission Framework dependency or integration.
The terrain-specific AI world is a placed mission entity, not a runtime spawn.
Save the world normally to persist it; one Undo removes the complete placement.

The initial scaffold has US/USSR factions. Authors still configure playable
groups, briefing content, objectives, setup zones and the separate mission
header for their scenario. Keep the systems root at the origin; its AI navigation
resources belong to the terrain's coordinate system.

## Terrain mapping

`GC_MissionTemplatePlugin.GetAIWorldPrefab` owns the explicit mapping:

| Terrain-owning scene | AI-world prefab |
| --- | --- |
| `worlds/Eden/Eden.ent` | `Prefabs/AI/SCR_AIWorld_Eden.et` |
| `worlds/Arland/Arland.ent` | `Prefabs/AI/SCR_AIWorld_Arland.et` |
| `worlds/Cain/Cain.ent` | `Prefabs/AI/SCR_AIWorld_Cain.et` |

Detection uses the loaded terrain entity's subscene through the editor's world,
not the mission filename. A renamed mission or an extra inherited mission layer
therefore does not change the map selection. Resource paths are matched exactly
after slash/case normalization. For another terrain, add its owning scene path
and GUID-qualified AI prefab to this mapping. Unknown scenes are refused without
placing anything; there is no default map or guessed prefab filename.

The command refuses prefab mode, Play, locked layers and placement into the base
terrain subscene. Existing game modes, AI worlds, faction/radio/chat/perception
managers are conflicts, including inherited layers
and prefab children. It reports the first conflict without deleting, replacing
or reparenting existing work. Failed AI placement removes the new root; if cleanup
fails, the message directs the author to inspect or Undo before retrying.
