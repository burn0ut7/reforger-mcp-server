from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionFlag.c';s=p.read_text();s=s.replace('\tint m_iStartingValue;','''	int m_iStartingValue;
	[Attribute("0", UIWidgets.CheckBox, "Off: accept one call and stay On. On: turn On for one check cycle, then reset to Off so a later call can activate it again. The remembered call count never resets here.")]
	bool m_bRepeatable;
	[Attribute("", UIWidgets.Object, "Requirements for accepting a call. No conditions means always true. Check this flag's Call Count with Less than 5 to allow five calls. Conditions are checked before increasing the count. Conditions referencing other flags can activate this flag when those flags change; a check of its own count only limits calls.")]
	ref array<ref GC_MissionCondition> m_aConditions;

	//------------------------------------------------------------------------------------------------
	GC_RuleCondition BuildConditions()
	{
		if (!m_aConditions || m_aConditions.IsEmpty())
			return null;
		return GC_MissionCondition.BuildGroup(m_aConditions, GC_ERuleCheck.ALL, 0, 0);
	}''');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text().replace('SELECT_FLAGS\n}', 'SELECT_FLAGS,\n\tCALL_FLAG\n}').replace('if (m_eDo == GC_ERuleAction.RANDOM_FLAG)','if (m_eDo == GC_ERuleAction.CALL_FLAG)\n\t\t\treturn !m_sTextOrName.IsEmpty();\n\t\tif (m_eDo == GC_ERuleAction.RANDOM_FLAG)').replace('float m_fChance = 1;', 'float m_fChance = 1;\n\tbool m_bObserveFlags;');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('if (rule.m_If.UsesGameModeState())','rule.m_bObserveFlags = rule.m_If.UsesFlag();\n\t\t\tif (rule.m_If.UsesGameModeState() || rule.m_bObserveFlags)');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionAction.c';s=p.read_text();s+='''
[BaseContainerProps(), BaseContainerCustomStringTitleField("Call Flag")]
class GC_CallFlagAction : GC_MissionAction
{
	[Attribute("", UIWidgets.EditBox, "The exact name in Mission Flags. If its conditions pass, increase its count, turn it On and check dependent events and flags. Repeatable flags reset to Off after the check cycle.")]
	string m_sFlagName;

	//------------------------------------------------------------------------------------------------
	override GC_RuleAction BuildAction()
	{
		GC_RuleAction action = new GC_RuleAction();
		action.m_eDo = GC_ERuleAction.CALL_FLAG;
		action.m_sTextOrName = m_sFlagName;
		return action;
	}
}
''';s=s.replace('Choose the new setting for this flag.', 'On calls a declared Mission Flag and respects its conditions, count and Repeatable setting. Off clears its On/Off value without resetting its count. Undeclared flags keep simple On/Off behavior.');p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text().replace('Optional. Name flags and choose their starting values. Mission events do not need flags. Undeclared flags start Off.', 'Optional mission memory. Each flag remembers accepted calls. Set Repeatable and Conditions to control future calls; Call Flag notifies dependent events and flags.');needle='\tint GetRunCount()';i=s.index(needle);s=s[:i]+'''	//! Trusted server extension point, not a player RPC.
	bool CallFlag(string name)
	{
		if (!HasMissionRuleAuthority() || !m_bMissionEventsEnabled || !m_MissionRuleEngine)
			return false;
		return m_MissionRuleEngine.CallFlag(name);
	}

	//------------------------------------------------------------------------------------------------
	int GetFlagCallCount(string name)
	{
		if (!m_MissionRuleEngine)
			return 0;
		return m_MissionRuleEngine.GetFlagCallCount(name);
	}

	//------------------------------------------------------------------------------------------------
'''+s[i:];p.write_text(s)
