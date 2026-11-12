from pathlib import Path
import re
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionCondition.c';s=p.read_text();a=s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("All');b=s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("True',a)
s=s[:a]+'''//! Combine child answers without a numeric threshold.
[BaseContainerProps(), BaseContainerCustomStringTitleField("Condition Group")]
class GC_ConditionGroup : GC_MissionCondition
{
	[Attribute("0", UIWidgets.ComboBox, "All: every condition is true. Any: at least one is true. None: every condition is false. Not all: at least one is false.", enums: {ParamEnum("All are true (AND)", "0"), ParamEnum("Any are true (OR)", "1"), ParamEnum("None are true (NOR)", "2"), ParamEnum("Not all are true (NAND)", "3")})]
	int m_iMatch;

	[Attribute("", UIWidgets.Object, "Conditions to combine. Each entry can contain another group or a Condition Count. Add at least one condition. None with one child reverses that child's answer.")]
	ref array<ref GC_MissionCondition> m_aConditions;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (!m_aConditions || m_aConditions.IsEmpty())
			return null;

		GC_ERuleCheck operation;
		switch (m_iMatch)
		{
			case 0:
				operation = GC_ERuleCheck.ALL;
				break;
			case 1:
				operation = GC_ERuleCheck.ANY;
				break;
			case 2:
				operation = GC_ERuleCheck.NONE;
				break;
			case 3:
				operation = GC_ERuleCheck.NOT_ALL;
				break;
			default:
				return null;
		}
		return BuildGroup(m_aConditions, operation, 0, depth);
	}
}

'''+s[b:];p.write_text(s)
for file in ['GC_StandardLogicTests.c','GC_MissionRuleTests.c']:
 p=r/'tests/McpTestAddon/scripts/Game'/file;s=p.read_text()
 for old,mode in [('GC_AllConditions',0),('GC_AnyCondition',1),('GC_NoneConditions',2),('GC_NotAllConditions',3)]:
  s=re.sub(r'(\t+)'+old+r' (\w+) = new '+old+r'\(\);',lambda m:m[1]+'GC_ConditionGroup '+m[2]+' = new GC_ConditionGroup();\n'+m[1]+m[2]+'.m_iMatch = '+str(mode)+';',s)
 if file=='GC_StandardLogicTests.c':
  i=s.index('\t\tGC_ConditionCount equal')
  s=s[:i]+'''		GC_BooleanCondition validLeaf = new GC_BooleanCondition();
		all.m_aConditions = {validLeaf};
		all.m_iMatch = -1;
		Check(!all.BuildCheck(), "negative group mode rejected");
		all.m_iMatch = 4;
		Check(!all.BuildCheck(), "unknown group mode rejected");

'''+s[i:]
 p.write_text(s)
p=r/'Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et';s=p.read_text().replace('GC_AllConditions','GC_ConditionGroup').replace('GC_NoneConditions "{7B115D5DFF7EDA42}" {','GC_ConditionGroup "{7B115D5DFF7EDA42}" {\n         m_iMatch 2');p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('Use **+** to choose an operation. Each group contains its own Conditions list, so groups can be nested and combined. There is no per-condition Type dropdown.','Use **+** to choose **Condition Group** or **Condition Count**. Both contain their own Conditions list, so they can be nested inside each other. A group has no number field; a count has a numeric threshold.').replace('| Condition object | Plain meaning |','**Condition Group > Match** offers four choices:\n\n| Match | Plain meaning |').replace('| At least one is true (OR) |','| Any are true (OR) |').replace('| At least one is false (NAND) |','| Not all are true (NAND) |').replace('375 checks passed with zero failures in Arland (187 standard logic, 148 interpreter, 30 authoring, six serialized nested-example and four flag-action example checks).','Compilation and runtime verification of the combined group/count update are pending.')
s+='''
### Updating to the combined Condition Group

All/Any/None/NotAll authoring classes are replaced by GC_ConditionGroup with m_iMatch (0 All, 1 Any, 2 None, 3 Not all) and the existing m_aConditions list. The shipped example is migrated. External saved objects need the same class and mode conversion. Group has no Required Count field; Condition Count remains separate. Both can nest inside either type.
''';p.write_text(s)
