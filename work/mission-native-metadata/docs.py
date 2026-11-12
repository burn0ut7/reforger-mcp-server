from pathlib import Path
import json
p=Path('companion/lobby2/authoring/MissionMetadata.md')
s=p.read_text().replace('and writes `<world>.ent.metadata.json` beside the world', 'and stores generated evidence in its `m_GC_GeneratedMetadata` native object')
s=s.replace('For an existing header, choose **Refresh metadata only** to save the world and refresh its metadata. Edit and save existing headers in Resource Manager.', 'For an existing header, save any Resource Manager edits first, then choose **Refresh metadata only**. It saves the world and replaces only the generated object in the matching registered mission header, preserving authored fields. Missing, incompatible or multiple matching headers are rejected. Headers are discovered under `Missions` and `Configs/Missions` in the world\'s addon. **Submit Mission** uses the same native writer.')
s=s.replace('The generated file contains content evidence, not authored objective tags or a complete dependency list. It does not edit the mission header. Include it with the published mission files if the website should use it. Packed-addon inclusion has not been verified.', 'The generated object contains content evidence, not authored objective tags or a complete dependency list. It is serialized by Enfusion into the registered `.conf` resource and follows the mission header through the normal addon resource pipeline. Publish the `.conf` and its existing `.conf.meta`; no generated JSON file is needed. A packed-addon round trip has not yet been verified.')
s=s.replace('## JSON contract', '## Native configuration contract')
mapping={'schemaVersion':'m_iSchemaVersion','world':'m_sWorld','scope':'m_sCaptureScope','limitations':'m_aLimitations','entityCount':'m_iEntityCount','factions':'m_aFactions','playerSlots':'m_aPlayerSlots','configuredFactions':'m_aConfiguredFactions','assets':'m_aAssets','prefabs':'m_aPrefabs','referencedAddonIds':'m_aReferencedAddonIds','loadedAddons':'m_aLoadedAddons','count':'m_iCount','sourceAddonIds':'m_aAddonIds','referencedByPlacedPrefab':'m_bReferencedByPlacedPrefab'}
for old,new in mapping.items(): s=s.replace('`'+old+'`','`'+new+'`')
s=s.replace('The website can consume this JSON directly. Rows and summaries are sorted for stable diffs. Existing mission-header metadata is not changed.', 'The website reads this native object and normalizes it into its internal catalogue record. `m_aStartHours`, `m_bRandomStartTime` and `m_bAllowHeaderTime` preserve starting-time evidence. Arrays use attributed Enforce classes; resource references use `ResourceName`. Enfusion owns serialization and embedded object IDs. The authored `m_GC_Catalog` object is not changed.\n\nMigration: refresh existing missions once, verify the embedded object, then remove their old `.ent.metadata.json` exports. The website no longer reads those exports; there is no fallback source.')
p.write_text(s)
p=Path('companion/lobby2/authoring/mission-catalogue.md')
s=p.read_text()
start=s.index('The existing Workbench scanner')
end=s.index('The picker labels',start)
s=s[:start]+'''The Workbench collector stores **generated content evidence** inside the scenario
header's native `m_GC_GeneratedMetadata` object. The website reads that object
from the same `.conf`; no separate world JSON is produced or consumed. Refresh
existing missions once before removing their old `.ent.metadata.json` exports.
It is a partial editor snapshot, not a complete dependency or player-equipment
inventory. Save header edits in Resource Manager, then use **GC > Create Mission
Data > Refresh metadata only**. **Submit Mission** also refreshes the snapshot
before review. Both preserve authored header settings.
''' +s[end:]
s=s.replace('Configs/Missions/ArlandLobby.conf','Configs/Missions/Gray/ArlandLobby.conf')
p.write_text(s)
root=Path(r'C:\Users\Gray\Documents\VS\GC-Website-2')
p=root/'docs/mission-catalog.md'
s=p.read_text().replace('`Header` and `Snapshot`','and `Header`').replace('Configs\\Missions\\ArlandLobby.conf','Configs\\Missions\\Gray\\ArlandLobby.conf')
start=s.index('Generated world content is optional and separate.')
end=s.index('\n\n',start)
s=s[:start]+'''Generated world content is optional and embedded in the native mission header as `m_GC_GeneratedMetadata GC_GeneratedMissionMetadata`. Its fields include `m_iSchemaVersion`, `m_sWorld`, `m_sCaptureScope`, limits, factions, slot rows, detected assets, addon provenance and starting-time evidence. The header hash covers generated changes. The importer verifies world identity/version and retains a bounded summary under `metadata.generated`; prefab detail rows are excluded from public catalogue responses. Native attributed defaults (including zero slot indexes and false booleans) are respected. Header reads are bounded to 8 MiB. Detected assets never become player equipment, player capacity, objective tags or complete required dependencies. The collector covers one mission subscene and excludes runtime spawners and other subscenes. Save/refresh before publishing; a repository commit does not prove the scan was refreshed. Old `.ent.metadata.json` files are no longer fetched. Refresh existing missions in Workbench, verify the embedded data, then remove those exports.''' +s[end:]
start=s.index('Optional `--mission-content')
end=s.index('\n\n',start)
s=s[:start]+'''Generated content is read directly from the supplied native header. `--mission-content` is retired and rejected. These are explicit local CLI file inputs, never remote-controlled server paths. The manifest retains stable local identity and historical world aliases; the header supplies mission metadata. This updates Arland without listing old repository missions or deleting history. The local test setup intentionally keeps repository sync disabled.''' +s[end:]
p.write_text(s)
p=root/'appsettings.Worker.Local.json'
s=p.read_text()
# Remove only the retired setting, preserving unrelated private settings and formatting.
import re
s=re.sub(r',?\n\s*"Snapshot"\s*:\s*"[^"\n]*"', '', s)
json.loads(s)
p.write_text(s)
