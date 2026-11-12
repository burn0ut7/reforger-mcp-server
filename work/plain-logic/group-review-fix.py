from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text();s=s.replace('GC_CompiledMissionRule rule = pending.m_Rule;\n\t\t\tif (!ConditionsPass(pending)', 'GC_CompiledMissionRule rule = pending.m_Rule;\n\t\t\t// Selected children inherit the parent event\'s match-end lifetime.\n\t\t\tif (pending.m_Parent)\n\t\t\t\trule = pending.m_Parent.m_Rule;\n\t\t\tif (!ConditionsPass(pending)').replace('&& !pending.m_Parent && rule.m_eWhen', '&& rule.m_eWhen');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_EventGroupTests.c';s=p.read_text();needle='\t\tgroup.m_iPickCount = 0;';s=s.replace(needle,'''\t\ta.m_Advanced.m_fWaitSeconds = 5;
		group = RandomGroup({a});
		group.m_aConditions = {};
		engine.Start({group.BuildRule()});
		engine.SetGameModeState(SCR_EGameModeState.POSTGAME, 2);
		engine.Advance(10);
		Check(!engine.GetFlag("delayed"), "default match-start group's child wait ends with match");

'''+needle);p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('class GC_RuleOptions\n{','//! Attribute defaults apply to Workbench deserialization. Script fixtures must set authored fields explicitly.\nclass GC_RuleOptions\n{');p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('508 checks passed (29 event-group/chance','509 checks passed (30 event-group/chance');s+='\nScript callers constructing authoring containers directly must set their fields explicitly (including Random Chance = 1). Attribute defaults apply when Workbench loads serialized containers. Compiled runtime rules default to full chance. Field initializers are intentionally avoided on attributed fields because they can overwrite serialized settings.\n';p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace('508 checks','509 checks');p.write_text(s)
