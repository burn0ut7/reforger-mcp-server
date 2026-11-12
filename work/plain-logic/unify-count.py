from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_NumberConditions.c';s=p.read_text().replace('//! Count satisfied child conditions. Each child may itself contain another group.', '//! One count comparison for child conditions or remembered mission-flag calls.').replace('class GC_ConditionCount : GC_MissionCondition\n{', '''class GC_ConditionCount : GC_MissionCondition
{
	[Attribute("0", UIWidgets.ComboBox, "Choose what to count: conditions that are true, or accepted calls to a mission flag.", enums: {ParamEnum("Conditions that are true", "0"), ParamEnum("Mission flag calls", "1")})]
	int m_iCountFrom;
''').replace('Compare the number of true conditions with Required Count.', 'Compare the chosen count with Required Count.').replace('The number to compare against. This counts true conditions, not people or entities.', 'The number to compare against. For conditions, use 0 to 256. For flag calls, use 0 to 1000000. Less than 5 on a repeatable flag allows five accepted calls.').replace('"0 256 1")]', '"0 1000000 1")]').replace('\t[Attribute("", UIWidgets.Object, "Conditions to count.', '''	[Attribute("", UIWidgets.EditBox, "Only used for Mission flag calls. Enter the exact name from Mission Flags. To limit the flag containing this condition, enter that flag's name.")]
	string m_sFlagName;

	[Attribute("", UIWidgets.Object, "Only used for Conditions that are true. Conditions to count.''')
s=s.replace('if (!m_aConditions || m_aConditions.IsEmpty() || m_iRequiredCount < 0 || m_iRequiredCount > 256)', '''if (depth > 16 || m_iComparison < 0 || m_iComparison > 3 || m_iRequiredCount < 0 || m_iRequiredCount > 1000000)
			return null;
		if (m_iCountFrom == 1)
		{
			if (m_sFlagName.IsEmpty())
				return null;
			GC_RuleCondition flagCheck = new GC_RuleCondition();
			flagCheck.m_iCheck = 4;
			flagCheck.m_sFlagName = m_sFlagName;
			flagCheck.m_iFlagComparison = m_iComparison;
			flagCheck.m_iFlagCount = m_iRequiredCount;
			if (m_iComparison == 3)
			{
				flagCheck.m_iFlagComparison = 2;
				flagCheck.m_bReverseAnswer = true;
			}
			return flagCheck;
		}
		if (m_iCountFrom != 0 || !m_aConditions || m_aConditions.IsEmpty() || m_iRequiredCount > 256)''');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_FlagConditions.c';s=p.read_text();s=s[:s.index('\n[BaseContainerProps(), BaseContainerCustomStringTitleField("Flag Call Count")]')];p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_FlagMemoryTests.c';s=p.read_text().replace('GC_FlagCallCountCondition','GC_ConditionCount').replace('check.m_iCount = count;', 'check.m_iRequiredCount = count;\n\t\tcheck.m_iCountFrom = 1;').replace('comparison < 3','comparison < 4').replace('if (comparison == 2)\n\t\t\t\t\texpected = 5 == number;', 'if (comparison == 2)\n\t\t\t\t\texpected = 5 == number;\n\t\t\t\tif (comparison == 3)\n\t\t\t\t\texpected = 5 != number;').replace('invalid = Calls("wave", 3, 5)', 'invalid = Calls("wave", 4, 5)');p.write_text(s)
p=r/'tests/McpTestAddon/Prefabs/FlagMemoryExample.et';s=p.read_text().replace('GC_FlagCallCountCondition','GC_ConditionCount').replace('GC_ConditionCount "{71620F377D834173}" {\n       m_sFlagName "wave"', 'GC_ConditionCount "{71620F377D834173}" {\n       m_iCountFrom 1\n       m_iComparison 1\n       m_iRequiredCount 5\n       m_sFlagName "wave"');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionFlag.c';s=p.read_text().replace("Check this flag's Call Count with Less than 5", "Use Condition Count, choose Mission flag calls, enter this flag's name and select Less than 5");p.write_text(s)
