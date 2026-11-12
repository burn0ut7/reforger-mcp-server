from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_RuleCondition.c';s=p.read_text().replace('int m_iRandomBehavior;', 'int m_iRandomBehavior;\n\tref array<string> m_aCasualtyFactions;\n\tint m_iCasualtyCount;\n\tfloat m_fCasualtyRatio;');s=s.replace('m_iCheck > 6', 'm_iCheck > 7');s=s.replace('\t\tif (m_iCheck == 6)\n\t\t\treturn m_fRandomInterval', '''		if (m_iCheck == 7)
		{
			if (m_iCasualtyCount < 0 || m_iCasualtyCount > 2 || !(m_fCasualtyRatio >= 0 && m_fCasualtyRatio <= 1))
				return false;
			if (m_aCasualtyFactions)
			{
				if (m_aCasualtyFactions.Count() > 256)
					return false;
				foreach (string faction : m_aCasualtyFactions)
				{
					if (faction.IsEmpty())
						return false;
				}
			}
			return true;
		}
		if (m_iCheck == 6)
			return m_fRandomInterval''',1)
pos=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool UsesRandom')
s=s[:pos]+'''	//------------------------------------------------------------------------------------------------
	bool UsesCasualties(int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 7)
			return true;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesCasualties(depth + 1))
					return true;
			}
		}
		return false;
	}

'''+s[pos:]
s=s.replace('map<GC_RuleCondition, bool> randomResults = null)', 'map<GC_RuleCondition, bool> randomResults = null, GC_CasualtySnapshot casualties = null)')
s=s.replace('EvaluateValid(flags, gameModeState, counts, events, randomResults)', 'EvaluateValid(flags, gameModeState, counts, events, randomResults, casualties)')
s=s.replace('map<GC_RuleCondition, bool> randomResults)\n\t{\n\t\tbool answer;', 'map<GC_RuleCondition, bool> randomResults, GC_CasualtySnapshot casualties)\n\t{\n\t\tbool answer;')
s=s.replace('else if (m_iCheck == 6)\n\t\t\trandomResults.Find', 'else if (m_iCheck == 7)\n\t\t\tanswer = casualties && casualties.Passes(m_aCasualtyFactions, m_iCasualtyCount, m_fCasualtyRatio);\n\t\telse if (m_iCheck == 6)\n\t\t\trandomResults.Find');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text().replace('RANDOM_CHECK\n','RANDOM_CHECK,\n\tCASUALTIES_CHANGED\n');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text().replace('protected int m_iRuns;', 'protected ref GC_CasualtySnapshot m_Casualties;\n\tprotected int m_iRuns;',1);s=s.replace('array<string> authoredEventNames = null)', 'array<string> authoredEventNames = null, GC_CasualtySnapshot casualties = null)');s=s.replace('Stop();\n\t\tm_aStates.Clear();','Stop();\n\t\tm_Casualties = casualties;\n\t\tm_aStates.Clear();',1);s=s.replace('m_mRandomResults.Clear();\n\t\tm_aEvents.Clear();','m_mRandomResults.Clear();\n\t\tm_Casualties = null;\n\t\tm_aEvents.Clear();',1)
s=s.replace('m_mEventResults, m_mRandomResults)', 'm_mEventResults, m_mRandomResults, m_Casualties)')
s=s.replace('state.m_Rule.m_If.UsesRandom()))', '(state.m_Rule.m_If.UsesRandom() || state.m_Rule.m_If.UsesCasualties())))')
s=s.replace('bool randomCheck = notification.m_eKind', 'bool casualtyChanged = notification.m_eKind == GC_ERuleEvent.CASUALTIES_CHANGED;\n\t\t\tbool randomCheck = notification.m_eKind')
s=s.replace('|| (randomCheck && memory.m_Condition.UsesRandom(notification.m_aRandomChecks))', '|| (randomCheck && memory.m_Condition.UsesRandom(notification.m_aRandomChecks))\n\t\t\t\t\t|| (casualtyChanged && memory.m_Condition.UsesCasualties())')
s=s.replace('|| (randomCheck && rule.m_If.UsesRandom(notification.m_aRandomChecks))))','|| (randomCheck && rule.m_If.UsesRandom(notification.m_aRandomChecks))\n\t\t\t\t\t|| (casualtyChanged && rule.m_If.UsesCasualties())))')
s=s.replace('if (state == SCR_EGameModeState.GAME)\n\t\t\tm_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.MATCH_BEGINS, ""));','''if (state == SCR_EGameModeState.GAME)
		{
			m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.MATCH_BEGINS, ""));
			if (m_Casualties)
				m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.CASUALTIES_CHANGED, ""));
		}''')
pos=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool GetEventResult')
s=s[:pos]+'''	//------------------------------------------------------------------------------------------------
	//! The owner supplies the frozen starting cohort before Match entry, then notifies confirmed deaths.
	void SetCasualties(GC_CasualtySnapshot casualties, bool notify = true)
	{
		m_Casualties = casualties;
		if (notify && m_bRunning && m_eGameModeState == SCR_EGameModeState.GAME)
			QueueEvent(GC_ERuleEvent.CASUALTIES_CHANGED, "casualties");
	}

'''+s[pos:];p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text().replace('protected ref GC_MissionRuleEngine m_MissionRuleEngine;', 'protected ref GC_MissionRuleEngine m_MissionRuleEngine;\n\tprotected ref GC_MissionCasualtyTracker m_MissionCasualties;');s=s.replace('m_MissionRuleEngine.Start(runtimeRules, m_aMissionFlags, GetLobbyPhase(), eventNames);','CaptureMissionCasualties(GetLobbyPhase());\n\t\tGC_CasualtySnapshot casualties;\n\t\tif (m_MissionCasualties)\n\t\t\tcasualties = m_MissionCasualties.GetSnapshot();\n\t\tm_MissionRuleEngine.Start(runtimeRules, m_aMissionFlags, GetLobbyPhase(), eventNames, casualties);');s=s.replace('StartMissionRules();\n\t\tif (m_MissionRuleEngine)', 'StartMissionRules();\n\t\tCaptureMissionCasualties(state);\n\t\tif (m_MissionRuleEngine)',1)
pos=s.index('\t//------------------------------------------------------------------------------------------------\n\tprotected void TickMissionRules')
s=s[:pos]+'''	//------------------------------------------------------------------------------------------------
	protected void CaptureMissionCasualties(SCR_EGameModeState state)
	{
		if (state != SCR_EGameModeState.GAME || m_MissionCasualties || !HasMissionRuleAuthority())
			return;
		m_MissionCasualties = new GC_MissionCasualtyTracker();
		m_MissionCasualties.Capture(this);
		if (m_MissionRuleEngine)
			m_MissionRuleEngine.SetCasualties(m_MissionCasualties.GetSnapshot(), false);
	}

'''+s[pos:]
s=s.replace('m_MissionRuleEngine.Stop();\n\t}', 'm_MissionRuleEngine.Stop();\n\t\tm_MissionCasualties = null;\n\t}',1)
s=s.replace('IEntity victim = instigatorContextData.GetVictimEntity();\n\t\tGC_Slot slot', '''IEntity victim = instigatorContextData.GetVictimEntity();
		if (HasMissionRuleAuthority() && m_bMissionEventsEnabled && GetLobbyPhase() == SCR_EGameModeState.GAME
			&& m_MissionRuleEngine && m_MissionRuleEngine.IsRunning() && m_MissionCasualties && m_MissionCasualties.RecordDeath(victim))
			m_MissionRuleEngine.SetCasualties(m_MissionCasualties.GetSnapshot());
		GC_Slot slot''',1);p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_ConditionBase.c';s=p.read_text().replace('Random Chance, Condition Group', 'Random Chance, Casualties, Condition Group');p.write_text(s)
