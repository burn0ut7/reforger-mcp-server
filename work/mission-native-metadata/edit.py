from pathlib import Path

addon = Path(r'C:\Users\Gray\Documents\My Games\ArmaReforgerWorkbench\addons\Lobby2')
header = addon / 'scripts/Game/Mission/GC_MissionHeader.c'
s = header.read_text()
s = s.replace('//! Native metadata schema. Automatic population is currently an optional test-addon experiment.', '//! Mission catalogue settings and generated content evidence use native config serialization.')
s = s.replace('\tstring m_sCaptureScope;', '''\tstring m_sCaptureScope;
\t[Attribute()]
\tref array<string> m_aLimitations;
\t[Attribute()]
\tref array<ref GC_MissionMetadataSlot> m_aPlayerSlots;
\t[Attribute()]
\tref array<int> m_aStartHours;
\t[Attribute()]
\tbool m_bRandomStartTime;
\t[Attribute()]
\tbool m_bAllowHeaderTime;''')
s = s[:s.index('\n#ifdef WORKBENCH')] + '\n}\n'
s = s.replace('//! Shared data model for the native config and optional JSON export.', '//! Placed prefab evidence; presence does not imply player availability.')
s += '''
[BaseContainerProps()]
class GC_MissionMetadataSlot
{
\t[Attribute()]
\tstring m_sGroup;
\t[Attribute()]
\tint m_iIndex;
\t[Attribute()]
\tstring m_sFaction;
\t[Attribute()]
\tResourceName m_sPrefab;
\t[Attribute()]
\tstring m_sName;
\t[Attribute()]
\tstring m_sLoadoutFaction;
}
'''
header.write_text(s)
p = addon / 'scripts/Game/Mission/GC_MissionMetadataExport.c'
s = p.read_text()
s = s.replace('\t\tGC_GeneratedMissionMetadata snapshot =', '\t\tif (!m_bComplete)\n\t\t\treturn null;\n\t\tGC_GeneratedMissionMetadata snapshot =', 1)
start = s.index('\t\tsnapshot.m_sCaptureScope =')
end = s.index('\n\t\tsnapshot.m_iEntityCount', start)
s = s[:start] + '''\t\tsnapshot.m_sCaptureScope = "world_subscene";
\t\tsnapshot.m_aLimitations = {"Captured mission subscene and prefab children only; other subscenes and runtime spawns excluded.", "Editor snapshot; refresh explicitly after saving mission changes.", "Player slots describe authored playable groups and loadout prefabs; random variants and inventory contents are not expanded.", "Prefab source addons are content evidence, not a complete dependency list.", "Asset presence does not imply player availability; mod configuration may be inaccurate."};
\t\tsnapshot.m_aPlayerSlots = {};
\t\tsnapshot.m_aPlayerSlots.Copy(m_aPlayerSlots);
\t\tsnapshot.m_aStartHours = {};
\t\tsnapshot.m_aStartHours.Copy(m_aStartHours);
\t\tsnapshot.m_bRandomStartTime = m_bRandomStart;
\t\tsnapshot.m_bAllowHeaderTime = m_bAllowHeaderTime;''' + s[end:]
s = s[:s.index('\n\t//------------------------------------------------------------------------------------------------\n\tbool Save(')] + '\n}\n#endif\n'
p.write_text(s)
