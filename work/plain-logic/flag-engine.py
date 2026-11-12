from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework/GC_MissionRuleEngine.c');s=p.read_text();s=s.replace('string m_sName;\n\n\tvoid GC_MissionRuleEvent', 'string m_sName;\n\tbool m_bFlagCall;\n\n\tvoid GC_MissionRuleEvent').replace('GC_ERuleEvent kind, string name)', 'GC_ERuleEvent kind, string name, bool flagCall = false)',1).replace('m_sName = name;','m_sName = name;\n\t\tm_bFlagCall = flagCall;',1)
s=s.replace('//! Runtime state is separate', '''//! Runtime flag memory is separate from authored containers.
class GC_MissionFlagState
{
	string m_sName;
	bool m_bRepeatable;
	ref GC_RuleCondition m_Condition;
}

//! Runtime state is separate''')
s=s.replace('protected ref map<string, bool> m_mFlags = new map<string, bool>();','''protected ref map<string, bool> m_mFlags = new map<string, bool>();
	protected ref map<string, int> m_mFlagCounts = new map<string, int>();
	protected ref map<string, ref GC_MissionFlagState> m_mFlagDefinitions = new map<string, ref GC_MissionFlagState>();
	protected ref array<string> m_aCalledFlags = {};
	protected ref array<string> m_aPulseFlags = {};
	protected ref array<GC_MissionRuleState> m_aFlagRuns = {};''')
s=s.replace('m_mFlags.Clear();\n\t\tm_fNow', 'm_mFlags.Clear();\n\t\tm_mFlagCounts.Clear();\n\t\tm_mFlagDefinitions.Clear();\n\t\tm_fNow',1)
s=s.replace('m_mFlags.Set(flag.m_sName, flag.m_iStartingValue == 1);','''GC_RuleCondition condition = flag.BuildConditions();
				if (flag.m_aConditions && !flag.m_aConditions.IsEmpty() && !condition)
				{
					m_mFlags.Clear();
					m_mFlagDefinitions.Clear();
					m_iInvalidRules = 1;
					Print("GC | Mission Framework | Invalid flag conditions; not started.", LogLevel.ERROR);
					return;
				}
				GC_MissionFlagState memory = new GC_MissionFlagState();
				memory.m_sName = flag.m_sName;
				memory.m_bRepeatable = flag.m_bRepeatable;
				memory.m_Condition = condition;
				m_mFlagDefinitions.Set(flag.m_sName, memory);
				m_mFlags.Set(flag.m_sName, flag.m_iStartingValue == 1);''')
s=s.replace('m_bRunning = false;\n\t\tm_aEvents.Clear();', 'm_bRunning = false;\n\t\tm_aEvents.Clear();\n\t\tResetPulses();',1)
needle='\tbool SetFlag(string name, bool answer)'
i=s.index(needle)
s=s[:i]+'''	int GetFlagCallCount(string name)
	{
		int count;
		m_mFlagCounts.Find(name, count);
		return count;
	}

	//------------------------------------------------------------------------------------------------
	protected bool CanCallFlag(string name)
	{
		GC_MissionFlagState memory;
		if (!m_bRunning || !m_mFlagDefinitions.Find(name, memory) || m_aCalledFlags.Contains(name))
			return false;
		if (!memory.m_bRepeatable && (GetFlag(name) || GetFlagCallCount(name) > 0))
			return false;
		if (GetFlagCallCount(name) == int.MAX)
			return false;
		return !memory.m_Condition || memory.m_Condition.Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts);
	}

	//------------------------------------------------------------------------------------------------
	//! True means queued; queued calls recheck conditions when handled.
	bool CallFlag(string name)
	{
		if (!CanCallFlag(name))
			return false;
		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return false;
		}
		m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.FLAG_ON, name, true));
		DrainEvents();
		return m_bRunning;
	}

	//------------------------------------------------------------------------------------------------
	protected void AcceptFlagCall(string name)
	{
		if (!CanCallFlag(name))
			return;
		GC_MissionFlagState memory = m_mFlagDefinitions.Get(name);
		m_aCalledFlags.Insert(name);
		m_mFlagCounts.Set(name, GetFlagCallCount(name) + 1);
		m_mFlags.Set(name, true);
		if (memory.m_bRepeatable)
			m_aPulseFlags.Insert(name);
		m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.FLAG_ON, name));
	}

	//------------------------------------------------------------------------------------------------
	protected void ResetPulses()
	{
		foreach (string name : m_aPulseFlags)
			m_mFlags.Set(name, false);
		m_aPulseFlags.Clear();
		m_aCalledFlags.Clear();
		m_aFlagRuns.Clear();
	}

	//------------------------------------------------------------------------------------------------
'''+s[i:]
s=s.replace('if (GetFlag(name) == answer)\n', 'if (answer && m_mFlagDefinitions.Contains(name))\n\t\t\treturn CallFlag(name);\n\t\tif (GetFlag(name) == answer)\n',1)
s=s.replace('if (!watched)\n\t\t\treturn true;','''if (!watched)
		{
			foreach (GC_MissionRuleState observer : m_aStates)
			{
				if (observer.m_bValid && observer.m_Rule.m_bObserveFlags && observer.m_Rule.m_If.UsesFlag(name))
					watched = true;
			}
			foreach (string key, GC_MissionFlagState flagObserver : m_mFlagDefinitions)
			{
				if (key != name && flagObserver.m_Condition && flagObserver.m_Condition.UsesFlag(name))
					watched = true;
			}
		}
		if (!watched)
			return true;''',1)
s=s.replace('state.m_Rule.m_If.Evaluate(m_mFlags, m_eGameModeState)', 'state.m_Rule.m_If.Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts)')
s=s.replace('case GC_ERuleAction.SET_FLAG:', 'case GC_ERuleAction.CALL_FLAG:\n\t\t\t\t\tCallFlag(action.m_sTextOrName);\n\t\t\t\t\tbreak;\n\t\t\t\tcase GC_ERuleAction.SET_FLAG:')
s=s.replace('GC_MissionRuleEvent notification = m_aEvents[0];\n\t\t\tm_aEvents.RemoveOrdered(0);', '''GC_MissionRuleEvent notification = m_aEvents[0];
			m_aEvents.RemoveOrdered(0);
			if (notification.m_bFlagCall)
			{
				AcceptFlagCall(notification.m_sName);
				continue;
			}
			bool flagChanged = notification.m_eKind == GC_ERuleEvent.FLAG_ON || notification.m_eKind == GC_ERuleEvent.FLAG_OFF;
			foreach (string flagName, GC_MissionFlagState memory : m_mFlagDefinitions)
			{
				if (!memory.m_Condition)
					continue;
				if ((flagChanged && flagName != notification.m_sName && memory.m_Condition.UsesFlag(notification.m_sName))
					|| (notification.m_eKind == GC_ERuleEvent.GAME_MODE_STATE && memory.m_Condition.UsesGameModeState()))
					CallFlag(flagName);
			}''')
s=s.replace('GC_CompiledMissionRule rule = state.m_Rule;\n\t\t\t\tif (rule.m_eWhen', '''GC_CompiledMissionRule rule = state.m_Rule;
				if (flagChanged && rule.m_bObserveFlags && rule.m_If && rule.m_If.UsesFlag(notification.m_sName))
				{
					// One eligible attempt per flag chain; false checks may pass after another flag changes.
					if (!m_aFlagRuns.Contains(state) && CanRun(state))
					{
						m_aFlagRuns.Insert(state);
						TryRun(state);
					}
					continue;
				}
				if (rule.m_eWhen''')
s=s.replace('\t\tm_bDispatching = false;\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tprotected void StopEventLoop()', '\t\tResetPulses();\n\t\tm_bDispatching = false;\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tprotected void StopEventLoop()')
p.write_text(s)
