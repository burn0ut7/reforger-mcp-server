from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('class GC_EventDefinition\n{', '''class GC_EventDefinition
{
	//------------------------------------------------------------------------------------------------
	//! Keep names even when an authored definition cannot compile.
	void CollectEventNames(array<string> names)
	{
	}
''');needle='\t//------------------------------------------------------------------------------------------------\n\toverride GC_CompiledMissionRule BuildRule()';method='''	//------------------------------------------------------------------------------------------------
	override void CollectEventNames(array<string> names)
	{
		if (!m_sName.IsEmpty())
			names.Insert(m_sName);
	}

''';s=s.replace(needle,method+needle,1);start=s.index('class GC_RandomEventGroup');idx=s.index(needle,start);s=s[:idx]+'''	//------------------------------------------------------------------------------------------------
	override void CollectEventNames(array<string> names)
	{
		if (!m_sName.IsEmpty())
			names.Insert(m_sName);
		if (!m_aEvents)
			return;
		foreach (GC_Event child : m_aEvents)
		{
			if (child)
				child.CollectEventNames(names);
		}
	}

'''+s[idx:];p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text().replace('array<ref GC_CompiledMissionRule> runtimeRules = {};','array<ref GC_CompiledMissionRule> runtimeRules = {};\n\t\tarray<string> eventNames = {};').replace('GC_CompiledMissionRule rule = null;\n\t\t\tif (definition', 'GC_CompiledMissionRule rule = null;\n\t\t\tif (definition)\n\t\t\t\tdefinition.CollectEventNames(eventNames);\n\t\t\tif (definition').replace('Start(runtimeRules, m_aMissionFlags, GetLobbyPhase())','Start(runtimeRules, m_aMissionFlags, GetLobbyPhase(), eventNames)');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text().replace('bool m_bFlagClear;', 'bool m_bFlagClear;\n\tbool m_bInitialState;').replace('SCR_EGameModeState initialState = -1)', 'SCR_EGameModeState initialState = -1, array<string> authoredEventNames = null)').replace('RegisterEventNames();','RegisterEventNames(authoredEventNames);').replace('protected void RegisterEventNames()', 'protected void RegisterEventNames(array<string> authoredEventNames)');a=s.index('\t\tmap<string, int> names =',s.index('protected void RegisterEventNames'));b=s.index('\n\t\tforeach (GC_MissionRuleState state : m_aStates)',a);e=s.index('\n\t\tforeach (GC_MissionRuleState state : m_aStates)',b+1);s=s[:a]+'''		array<string> suppliedNames = authoredEventNames;
		if (!suppliedNames)
		{
			suppliedNames = {};
			foreach (GC_MissionRuleState state : m_aStates)
			{
				if (state.m_Rule)
					suppliedNames.Insert(state.m_Rule.m_sName);
			}
		}
		map<string, int> names = new map<string, int>();
		foreach (string name : suppliedNames)
		{
			if (name.IsEmpty())
				continue;
			int count;
			names.Find(name, count);
			names.Set(name, count + 1);
		}'''+s[e:];s=s.replace('\t\tm_eGameModeState = state;','\t\tbool initialObservation = !GC_LobbyPhase.IsValid(m_eGameModeState);\n\t\tm_eGameModeState = state;',1).replace('m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.GAME_MODE_STATE, ""));', 'GC_MissionRuleEvent observation = new GC_MissionRuleEvent(GC_ERuleEvent.GAME_MODE_STATE, "");\n\t\tobservation.m_bInitialState = initialObservation;\n\t\tm_aEvents.Insert(observation);',1)
s=s.replace('''		if (!CanRun(state))
			return;
		float chance''','''		if (!CanRun(state))
			return;
		if (state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents)
		{
			if (m_aReactiveRuns.Contains(state))
				return;
			m_aReactiveRuns.Insert(state);
		}
		float chance''',1)
s=s.replace('''					if (!m_aReactiveRuns.Contains(state) && CanRun(state))
					{
						m_aReactiveRuns.Insert(state);
						TryRun(state);
					}''','\t\t\t\t\tTryRun(state);');needle='\t\t\t\tif (rule.m_eWhen == notification.m_eKind';s=s.replace(needle,'''\t\t\t\t// An event-result reference does not subscribe to unrelated phase changes.
				if (notification.m_eKind == GC_ERuleEvent.GAME_MODE_STATE && !notification.m_bInitialState
					&& rule.m_bObserveEvents && rule.m_If && !rule.m_If.UsesGameModeState())
					continue;
'''+needle);p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('Is true/Is Off', 'Is true/Is false');s=s.replace('Events are checked at initial setup and relevant state/flag/event notifications, not by continuously polling every condition.', 'Events are checked at initial setup and relevant state/flag/event notifications, not by continuously polling every condition. Event-only references do not recheck on unrelated phase changes; add Game Mode State when a phase change should also matter.');s+='\nDesign boundary: each mission event and flag owns its conditions, repeat behavior, delay and activation rules. Outside references read published results only. They never invoke the referenced object or re-evaluate its internal conditions. Duplicate-name detection includes authored entries that fail to compile, so incomplete duplicates cannot become ambiguous valid references.\n';p.write_text(s)
