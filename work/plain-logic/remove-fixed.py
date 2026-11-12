from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionCondition.c';s=p.read_text();s=s[:s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("True or false value")')].rstrip()+'\n';p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=p.read_text().replace('GC_BooleanCondition','GC_TestFixedCondition');s='''//! Script-only input for logic tests. No container or editor attributes: never authored in mission resources.
class GC_TestFixedCondition : GC_MissionCondition
{
	bool m_bValue;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16)
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 2;
		check.m_bTestAnswer = m_bValue;
		return check;
	}
}

'''+s
old='rules[0].m_If && rules[0].m_If.m_aChecks.Count() == 1 && rules[0].m_If.m_aChecks[0].m_aChecks.Count() == 3 && rules[0].m_If.m_aChecks[0].m_iHowMany == 1, "serialized two-out-of-three nested conditions are present"'
s=s.replace(old,'rules[0].m_If && rules[0].m_If.m_aChecks.IsEmpty(), "serialized example needs no placeholder conditions"');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_StandardLogicTests.c';s=p.read_text().replace('GC_BooleanCondition','GC_TestFixedCondition');p.write_text(s)
p=r/'Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et';s=p.read_text();a=s.index('     m_aConditions {');b=s.index('     m_Event ',a);s=s[:a]+s[b:];p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('This is the nested layout demonstrated by the no-flags example.','The optional script tests exercise this nested layout.').replace('A **True or false value** supplies a fixed input for testing: checked = true, unchecked = false. It is not a flag or a live check.','The fixed **True or false value** placeholder has been removed. Script-only test inputs live in the optional test addon and are not authored as mission conditions. Until special checks arrive, leave Conditions empty for unrestricted events; empty explicit groups still fail closed.').replace('no flags; it demonstrates a two-out-of-three comparison containing All/None groups.','no flags and no conditions; it demonstrates an unrestricted timer event.').replace('inspecting the nested count example','inspecting the no-flags timer example').replace('six serialized nested-example','six serialized no-flags example').replace('The shipped two-of-three example now uses Greater than 1 and combined All/None groups.','The shipped no-flags example now has no conditions, preserving its timer and random action behavior without fixed Boolean placeholders.')
s+='''
The GC_BooleanCondition authoring class has been removed. External resources using it need deliberate removal or replacement with appropriate real checks when available; do not delete a false restriction without considering how that changes the event. No saved world in the inspected project referenced this class at removal.
''';p.write_text(s)
