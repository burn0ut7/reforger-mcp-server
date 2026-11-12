from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework')
p=r/'GC_FlagConditions.c'
p.write_text('''[BaseContainerProps(), BaseContainerCustomStringTitleField("Flag State")]
class GC_FlagStateCondition : GC_MissionCondition
{
	[Attribute("", UIWidgets.EditBox, "The flag to check. Use its exact name from Mission Flags.")]
	string m_sFlagName;
	[Attribute("0", UIWidgets.ComboBox, "On means the flag is active. A repeatable flag stays On only while its call is being handled, then resets to Off.", enums: {ParamEnum("Is On", "0"), ParamEnum("Is Off", "1")})]
	int m_iMatch;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16 || m_sFlagName.IsEmpty() || m_iMatch < 0 || m_iMatch > 1)
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 1;
		check.m_sFlagName = m_sFlagName;
		check.m_bReverseAnswer = m_iMatch == 1;
		return check;
	}
}

[BaseContainerProps(), BaseContainerCustomStringTitleField("Flag Call Count")]
class GC_FlagCallCountCondition : GC_MissionCondition
{
	[Attribute("", UIWidgets.EditBox, "The flag whose remembered call count to check. Use its exact name from Mission Flags.")]
	string m_sFlagName;
	[Attribute("1", UIWidgets.ComboBox, "Compare the remembered count with the number below.", enums: {ParamEnum("Greater than", "0"), ParamEnum("Less than", "1"), ParamEnum("Equal to", "2")})]
	int m_iComparison;
	[Attribute("5", UIWidgets.SpinBox, "The number of calls to compare against. This counts calls, not how many child conditions pass.", "0 1000000 1")]
	int m_iCount;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16 || m_sFlagName.IsEmpty() || m_iComparison < 0 || m_iComparison > 2 || m_iCount < 0 || m_iCount > 1000000)
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 4;
		check.m_sFlagName = m_sFlagName;
		check.m_iFlagComparison = m_iComparison;
		check.m_iFlagCount = m_iCount;
		return check;
	}
}
''')
p=r/'GC_RuleCondition.c';s=p.read_text().replace('SCR_EGameModeState m_eGameModeState;', 'SCR_EGameModeState m_eGameModeState;\n\tint m_iFlagComparison;\n\tint m_iFlagCount;')
s=s.replace('m_iCheck > 3','m_iCheck > 4').replace('if (m_iCheck == 3)\n\t\t\treturn GC_LobbyPhase', 'if (m_iCheck == 4)\n\t\t\treturn !m_sFlagName.IsEmpty() && m_iFlagComparison >= 0 && m_iFlagComparison <= 2 && m_iFlagCount >= 0 && m_iFlagCount <= 1000000;\n\t\tif (m_iCheck == 3)\n\t\t\treturn GC_LobbyPhase')
needle='\tbool Evaluate(map<string, bool> flags, SCR_EGameModeState gameModeState = -1)'
s=s.replace(needle,'''bool UsesFlag(string name = "", int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 1 || m_iCheck == 4)
			return name.IsEmpty() || m_sFlagName == name;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesFlag(name, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	bool Evaluate(map<string, bool> flags, SCR_EGameModeState gameModeState = -1, map<string, int> counts = null)''')
s=s.replace('EvaluateValid(flags, gameModeState)', 'EvaluateValid(flags, gameModeState, counts)').replace('protected bool EvaluateValid(map<string, bool> flags, SCR_EGameModeState gameModeState)', 'protected bool EvaluateValid(map<string, bool> flags, SCR_EGameModeState gameModeState, map<string, int> counts)')
s=s.replace('else if (m_iCheck == 3)\n\t\t\tanswer', '''else if (m_iCheck == 4)
		{
			int calls;
			if (counts)
				counts.Find(m_sFlagName, calls);
			if (m_iFlagComparison == 0)
				answer = calls > m_iFlagCount;
			else if (m_iFlagComparison == 1)
				answer = calls < m_iFlagCount;
			else
				answer = calls == m_iFlagCount;
		}
		else if (m_iCheck == 3)
			answer''');s=s.replace('\nbool UsesFlag', '\n\tbool UsesFlag');p.write_text(s)
