from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework/GC_MissionRuleEngine.c');s=p.read_text();s=s.replace('protected ref array<string> m_aPulseFlags = {};','''protected ref array<string> m_aPulseFlags = {};
	protected ref map<string, bool> m_mEventResults = new map<string, bool>();
	protected ref array<string> m_aPulseEvents = {};''').replace('m_mFlagDefinitions.Clear();\n\t\tm_fNow', 'm_mFlagDefinitions.Clear();\n\t\tm_mEventResults.Clear();\n\t\tm_fNow',1)
s=s.replace('\t\tm_bRunning = true;\n\t\tif (GC_LobbyPhase.IsValid(initialState))','\t\tRegisterEventNames();\n\t\tm_bRunning = true;\n\t\tif (GC_LobbyPhase.IsValid(initialState))',1)
i=s.index('\tprotected GC_MissionRuleState CreateState');s=s[:i]+'''	protected void RegisterEventNames()
	{
		map<string, int> names = new map<string, int>();
		foreach (GC_MissionRuleState state : m_aStates)
		{
			if (!state.m_Rule || state.m_Rule.m_sName.IsEmpty())
				continue;
			string name = state.m_Rule.m_sName;
			int count;
			names.Find(name, count);
			names.Set(name, count + 1);
		}
		foreach (GC_MissionRuleState state : m_aStates)
		{
			if (state.m_bValid && !state.m_Rule.m_sName.IsEmpty() && names.Get(state.m_Rule.m_sName) == 1)
				m_mEventResults.Set(state.m_Rule.m_sName, false);
		}
	}

	//------------------------------------------------------------------------------------------------
	bool GetEventResult(string name)
	{
		bool result;
		m_mEventResults.Find(name, result);
		return result;
	}

	//------------------------------------------------------------------------------------------------
	protected void PublishEventResult(GC_MissionRuleState state)
	{
		string name = state.m_Rule.m_sName;
		if (!m_bRunning || !m_mEventResults.Contains(name))
			return;
		m_mEventResults.Set(name, true);
		if (!state.m_Rule.m_bRunOnlyOnce && !m_aPulseEvents.Contains(name))
			m_aPulseEvents.Insert(name);
		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return;
		}
		m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.EVENT_RAN, name));
	}

	//------------------------------------------------------------------------------------------------
'''+s[i:]
s=s.replace('Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts)', 'Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts, m_mEventResults)')
s=s.replace('\t\tm_aPulseFlags.Clear();', '\t\tforeach (string eventName : m_aPulseEvents)\n\t\t\tm_mEventResults.Set(eventName, false);\n\t\tm_aPulseEvents.Clear();\n\t\tm_aPulseFlags.Clear();')
s=s.replace('\tprotected void Execute(GC_MissionRuleState state)\n\t{', '''	protected void Execute(GC_MissionRuleState state)
	{
		// A reactive event must not call itself again through its result in this cycle.
		if ((state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents) && !m_aFlagRuns.Contains(state))
			m_aFlagRuns.Insert(state);''')
needle='\n\t//------------------------------------------------------------------------------------------------\n\tprotected void ExecuteRandomGroup';i=s.index(needle);before=s[:i];pos=before.rfind('\n\t}');s=before[:pos]+'\n\t\tPublishEventResult(state);'+before[pos:]+s[i:]
s=s.replace('\t\tforeach (GC_MissionRuleState chosen : selected)\n\t\t\tTryRun(chosen);', '\t\tforeach (GC_MissionRuleState chosen : selected)\n\t\t\tTryRun(chosen);\n\t\tPublishEventResult(group);')
s=s.replace('bool flagChanged = notification.m_eKind', 'bool eventRan = notification.m_eKind == GC_ERuleEvent.EVENT_RAN;\n\t\t\tbool flagChanged = notification.m_eKind')
s=s.replace('|| (notification.m_eKind == GC_ERuleEvent.GAME_MODE_STATE && memory.m_Condition.UsesGameModeState()))', '|| (eventRan && memory.m_Condition.UsesEvent(notification.m_sName))\n\t\t\t\t\t|| (notification.m_eKind == GC_ERuleEvent.GAME_MODE_STATE && memory.m_Condition.UsesGameModeState()))')
s=s.replace('if (flagChanged && rule.m_bObserveFlags && rule.m_If && rule.m_If.UsesFlag(notification.m_sName))', '''if (rule.m_If && ((flagChanged && rule.m_bObserveFlags && rule.m_If.UsesFlag(notification.m_sName))
					|| (eventRan && rule.m_bObserveEvents && rule.m_If.UsesEvent(notification.m_sName))))''')
s=s.replace('// One eligible attempt per flag chain; false checks may pass after another flag changes.', '// One eligible attempt per notification chain; false checks may pass on a later notification.')
p.write_text(s)
