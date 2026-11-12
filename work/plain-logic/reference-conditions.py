from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
for folder in ['scripts','tests/McpTestAddon','Prefabs','worlds']:
 for p in (r/folder).rglob('*'):
  if p.suffix.lower() not in ['.c','.et','.layer','.ent','.conf']: continue
  b=p.read_bytes()
  if b'GC_FlagStateCondition' in b: p.write_bytes(b.replace(b'GC_FlagStateCondition',b'GC_MissionFlagCondition'))
p=r/'scripts/Game/MissionFramework/GC_FlagConditions.c';s=p.read_text().replace('StringTitleField("Flag State")','StringTitleField("Mission Flag Condition")').replace('On means the flag is active. A repeatable flag stays On only while its call is being handled, then resets to Off.', 'Check the flag\'s remembered result. True means it is active. Its own conditions control activation. A repeatable flag is true during its check cycle, then resets to false.').replace('ParamEnum("Is On", "0"), ParamEnum("Is Off", "1")','ParamEnum("Is true", "0"), ParamEnum("Is false", "1")');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionEventCondition.c';p.write_text('''[BaseContainerProps(), BaseContainerCustomStringTitleField("Mission Event Condition")]
class GC_MissionEventCondition : GC_MissionCondition
{
	[Attribute("", UIWidgets.EditBox, "The exact unique name of a regular mission event or random event group. Checks its result without running its actions or checking its conditions again.")]
	string m_sEventName;
	[Attribute("0", UIWidgets.ComboBox, "True means the named event ran. Once events remember true. Every time events are true for their check cycle, then reset to false. A random group is true when it makes a selection. Missing or duplicate event names never pass, including Is false.", enums: {ParamEnum("Is true", "0"), ParamEnum("Is false", "1")})]
	int m_iMatch;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16 || m_sEventName.IsEmpty() || m_iMatch < 0 || m_iMatch > 1)
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 5;
		check.m_sEventName = m_sEventName;
		check.m_bReverseAnswer = m_iMatch == 1;
		return check;
	}
}
''')
p=r/'scripts/Game/MissionFramework/GC_RuleCondition.c';s=p.read_text().replace('int m_iFlagCount;', 'int m_iFlagCount;\n\tstring m_sEventName;').replace('m_iCheck > 4','m_iCheck > 5').replace('if (m_iCheck == 4)\n\t\t\treturn', 'if (m_iCheck == 5)\n\t\t\treturn !m_sEventName.IsEmpty();\n\t\tif (m_iCheck == 4)\n\t\t\treturn',1)
i=s.index('\tbool Evaluate(map');s=s[:i]+'''	bool UsesEvent(string name = "", int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 5)
			return name.IsEmpty() || m_sEventName == name;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesEvent(name, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	protected bool HasEventContext(map<string, bool> events, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 5)
			return events && events.Contains(m_sEventName);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (!child || !child.HasEventContext(events, depth + 1))
					return false;
			}
		}
		return true;
	}

	//------------------------------------------------------------------------------------------------
'''+s[i:]
s=s.replace('map<string, int> counts = null)', 'map<string, int> counts = null, map<string, bool> events = null)').replace('if (!IsValid() || (UsesGameModeState()', 'if (!IsValid() || !HasEventContext(events) || (UsesGameModeState()').replace('EvaluateValid(flags, gameModeState, counts)', 'EvaluateValid(flags, gameModeState, counts, events)').replace('map<string, int> counts)\n', 'map<string, int> counts, map<string, bool> events)\n').replace('else if (m_iCheck == 4)\n', 'else if (m_iCheck == 5)\n\t\t\tevents.Find(m_sEventName, answer);\n\t\telse if (m_iCheck == 4)\n');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text().replace('GAME_MODE_STATE\n}', 'GAME_MODE_STATE,\n\tEVENT_RAN\n}').replace('bool m_bObserveFlags;', 'bool m_bObserveFlags;\n\tbool m_bObserveEvents;').replace('m_eWhen > GC_ERuleEvent.GAME_MODE_STATE','m_eWhen > GC_ERuleEvent.EVENT_RAN');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('rule.m_bObserveFlags = rule.m_If.UsesFlag();', 'rule.m_bObserveFlags = rule.m_If.UsesFlag();\n\t\t\trule.m_bObserveEvents = rule.m_If.UsesEvent();').replace('|| rule.m_bObserveFlags)', '|| rule.m_bObserveFlags || rule.m_bObserveEvents)');p.write_text(s)
