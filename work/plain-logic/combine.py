from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_NumberConditions.c'
p.write_text('''//! Count satisfied child conditions. Each child may itself contain another group.
[BaseContainerProps(), BaseContainerCustomStringTitleField("Condition Count")]
class GC_ConditionCount : GC_MissionCondition
{
	[Attribute("0", UIWidgets.ComboBox, "Compare the number of true conditions with Required Count. Greater than 2 needs 3 or more; Less than 2 allows 0 or 1; Equal to 2 needs exactly 2.", enums: {ParamEnum("Greater than (>)", "0"), ParamEnum("Less than (<)", "1"), ParamEnum("Equal to (=)", "2")})]
	int m_iComparison;

	[Attribute("1", UIWidgets.SpinBox, "The number to compare against. This counts true conditions, not people or entities.", "0 256 1")]
	int m_iRequiredCount;

	[Attribute("", UIWidgets.Object, "Conditions to count. Each entry gives one true or false answer and may contain other groups. Add at least one condition.")]
	ref array<ref GC_MissionCondition> m_aConditions;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (!m_aConditions || m_aConditions.IsEmpty() || m_iRequiredCount < 0 || m_iRequiredCount > 256)
			return null;

		GC_ERuleCheck operation;
		switch (m_iComparison)
		{
			case 0:
				operation = GC_ERuleCheck.GREATER_THAN;
				break;
			case 1:
				operation = GC_ERuleCheck.LESS_THAN;
				break;
			case 2:
				operation = GC_ERuleCheck.EXACTLY;
				break;
			default:
				return null;
		}
		return BuildGroup(m_aConditions, operation, m_iRequiredCount, depth);
	}
}
''')
p=r/'Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et';s=p.read_text().replace('GC_GreaterOrEqualCondition','GC_ConditionCount').replace('m_iRequiredCount 2','m_iRequiredCount 1');p.write_text(s)
p=r/'worlds/examples/arland/Arland_Lobby_Layers/default.layer';s=p.read_text().replace('GC_GreaterThanCondition','GC_ConditionCount');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=p.read_text().replace('m_iHowMany == 2','m_iHowMany == 1');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_StandardLogicTests.c';s=p.read_text();a=s.index('\t\tGC_EqualCondition');b=s.index('\t\tfor (int passed',a)
s=s[:a]+'''		GC_ConditionCount equal = new GC_ConditionCount();
		equal.m_iComparison = 2;
		GC_ConditionCount less = new GC_ConditionCount();
		less.m_iComparison = 1;
		GC_ConditionCount greater = new GC_ConditionCount();
		array<ref GC_MissionCondition> comparisons = {equal, less, greater};
		array<ref GC_MissionCondition> counted = {};
		equal.m_aConditions = counted;
		less.m_aConditions = counted;
		greater.m_aConditions = counted;
'''+s[b:]
for x in ['different','lessEqual','greaterEqual']: s=s.replace(f'\t\t\t\t{x}.m_iRequiredCount = required;\n','')
s=s.replace('{passed == required, passed != required, passed < required, passed <= required, passed > required, passed >= required}','{passed == required, passed < required, passed > required}').replace('greaterEqual','greater').replace('greater.m_iRequiredCount = 2;','greater.m_iRequiredCount = 1;')
pos=s.index('\t\tfor (int removed');s=s[:pos]+'''		greater.m_iRequiredCount = 1;
		greater.m_iComparison = -1;
		Check(!greater.BuildCheck(), "negative comparison rejected");
		greater.m_iComparison = 3;
		Check(!greater.BuildCheck(), "unknown comparison rejected");
'''+s[pos:];p.write_text(s)
