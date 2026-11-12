from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_RuleCondition.c'
s=p.read_text()
s=s.replace('string m_sEventName;', 'string m_sEventName;\n\tfloat m_fRandomInterval;\n\tfloat m_fRandomChance;')
s=s.replace('m_iCheck > 5', 'm_iCheck > 6')
s=s.replace('if (m_iCheck == 5)\n\t\t\treturn !m_sEventName', 'if (m_iCheck == 6)\n\t\t\treturn m_fRandomInterval >= 0.1 && m_fRandomInterval <= 86400 && m_fRandomChance >= 0 && m_fRandomChance <= 1;\n\t\tif (m_iCheck == 5)\n\t\t\treturn !m_sEventName',1)
pos=s.index('\tprotected bool HasEventContext')
s=s[:pos]+'''bool UsesRandom(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 6)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesRandom(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectRandomConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 6 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectRandomConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	'''+s[pos:]
s=s.replace('bool UsesRandom', '\tbool UsesRandom',1)
s=s.replace('HasEventContext(map<string, bool> events, int depth = 0)', 'HasEventContext(map<string, bool> events, map<GC_RuleCondition, bool> randomResults, int depth = 0)')
s=s.replace('if (m_iCheck == 5)\n\t\t\treturn events', 'if (m_iCheck == 6)\n\t\t\treturn randomResults && randomResults.Contains(this);\n\t\tif (m_iCheck == 5)\n\t\t\treturn events')
s=s.replace('child.HasEventContext(events, depth + 1)', 'child.HasEventContext(events, randomResults, depth + 1)')
s=s.replace('map<string, bool> events = null)', 'map<string, bool> events = null, map<GC_RuleCondition, bool> randomResults = null)')
s=s.replace('!HasEventContext(events)', '!HasEventContext(events, randomResults)')
s=s.replace('EvaluateValid(flags, gameModeState, counts, events)', 'EvaluateValid(flags, gameModeState, counts, events, randomResults)')
s=s.replace('map<string, bool> events)\n\t{', 'map<string, bool> events, map<GC_RuleCondition, bool> randomResults)\n\t{')
s=s.replace('else if (m_iCheck == 5)\n\t\t\tevents', 'else if (m_iCheck == 6)\n\t\t\trandomResults.Find(this, answer);\n\t\telse if (m_iCheck == 5)\n\t\t\tevents')
p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text().replace('EVENT_RAN\n','EVENT_RAN,\n\tRANDOM_CHECK\n');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('later matching state changes or flag calls','later matching state changes, flag calls or scheduled random checks').replace('rule.m_If.UsesGameModeState() || rule.m_bObserveFlags || rule.m_bObserveEvents','rule.m_If.UsesGameModeState() || rule.m_bObserveFlags || rule.m_bObserveEvents || rule.m_If.UsesRandom()');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text()
s=s.replace('bool m_bInitialState;', 'bool m_bInitialState;\n\tref array<GC_RuleCondition> m_aRandomChecks;')
s=s.replace('protected float m_fNow;', 'protected float m_fNow;\n\tprotected ref array<ref GC_RandomConditionState> m_aRandomConditions = {};\n\tprotected ref map<GC_RuleCondition, bool> m_mRandomResults = new map<GC_RuleCondition, bool>();')
s=s.replace('m_aStates.Clear();', 'm_aStates.Clear();\n\t\tm_aRandomConditions.Clear();',1)
s=s.replace('RegisterEventNames(authoredEventNames);','RegisterEventNames(authoredEventNames);\n\t\tRegisterRandomConditions();',1)
s=s.replace('m_eGameModeState = state;','m_eGameModeState = state;\n\t\tResetRandomConditions();',1)
s=s.replace('m_bRunning = false;\n\t\tm_aEvents.Clear();','m_bRunning = false;\n\t\tm_mRandomResults.Clear();\n\t\tm_aEvents.Clear();',1)
s=s.replace('Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts, m_mEventResults)', 'Evaluate(m_mFlags, m_eGameModeState, m_mFlagCounts, m_mEventResults, m_mRandomResults)')
s=s.replace('m_bDispatching = true;\n\t\tforeach (GC_MissionRuleState state : m_aStates)', 'm_bDispatching = true;\n\t\tAdvanceRandomConditions();\n\t\tforeach (GC_MissionRuleState state : m_aStates)',1)
s=s.replace('if (state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents)', 'if (state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents || (state.m_Rule.m_If && state.m_Rule.m_If.UsesRandom()))')
s=s.replace('(state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents) &&', '(state.m_Rule.m_bObserveFlags || state.m_Rule.m_bObserveEvents || (state.m_Rule.m_If && state.m_Rule.m_If.UsesRandom())) &&')
s=s.replace('bool eventRan = notification.m_eKind', 'bool randomCheck = notification.m_eKind == GC_ERuleEvent.RANDOM_CHECK;\n\t\t\tbool eventRan = notification.m_eKind')
s=s.replace('|| (eventRan && memory.m_Condition.UsesEvent(notification.m_sName))', '|| (eventRan && memory.m_Condition.UsesEvent(notification.m_sName))\n\t\t\t\t\t|| (randomCheck && memory.m_Condition.UsesRandom(notification.m_aRandomChecks))')
s=s.replace('|| (eventRan && rule.m_bObserveEvents && rule.m_If.UsesEvent(notification.m_sName))))', '|| (eventRan && rule.m_bObserveEvents && rule.m_If.UsesEvent(notification.m_sName))\n\t\t\t\t\t|| (randomCheck && rule.m_If.UsesRandom(notification.m_aRandomChecks))))')
pos=s.index('\t//------------------------------------------------------------------------------------------------\n\tprotected bool CanRun')
s=s[:pos]+'''	//------------------------------------------------------------------------------------------------
	protected void RegisterRandomConditions()
	{
		array<GC_RuleCondition> conditions = {};
		foreach (GC_MissionRuleState state : m_aStates)
		{
			if (state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_If)
				state.m_Rule.m_If.CollectRandomConditions(conditions);
		}
		foreach (string name, GC_MissionFlagState memory : m_mFlagDefinitions)
		{
			if (memory.m_Condition)
				memory.m_Condition.CollectRandomConditions(conditions);
		}
		foreach (GC_RuleCondition condition : conditions)
		{
			GC_RandomConditionState scheduled = new GC_RandomConditionState();
			scheduled.m_Condition = condition;
			m_aRandomConditions.Insert(scheduled);
		}
		ResetRandomConditions();
	}

	//------------------------------------------------------------------------------------------------
	protected void ResetRandomConditions()
	{
		m_mRandomResults.Clear();
		foreach (GC_RandomConditionState scheduled : m_aRandomConditions)
			scheduled.m_fNextCheck = m_fNow + scheduled.m_Condition.m_fRandomInterval;
	}

	//------------------------------------------------------------------------------------------------
	//! Roll every due leaf before notifying any owner, so nesting sees one consistent snapshot.
	protected void AdvanceRandomConditions()
	{
		if (m_eGameModeState != SCR_EGameModeState.GAME)
			return;
		GC_MissionRuleEvent notification = new GC_MissionRuleEvent(GC_ERuleEvent.RANDOM_CHECK, "");
		notification.m_aRandomChecks = {};
		foreach (GC_RandomConditionState scheduled : m_aRandomConditions)
		{
			if (scheduled.m_fNextCheck > m_fNow)
				continue;
			GC_RuleCondition condition = scheduled.m_Condition;
			float chance = condition.m_fRandomChance;
			bool result = chance >= 1 || (chance > 0 && RandomUnit() < chance);
			m_mRandomResults.Set(condition, result);
			// A late update rolls once, never bursts through missed intervals.
			scheduled.m_fNextCheck = m_fNow + condition.m_fRandomInterval;
			notification.m_aRandomChecks.Insert(condition);
		}
		if (!notification.m_aRandomChecks.IsEmpty())
			m_aEvents.Insert(notification);
	}

'''+s[pos:]
p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text().replace('if (m_aMissionEvents && !m_aMissionEvents.IsEmpty() && m_MissionRuleEngine.IsRunning())','if (m_MissionRuleEngine.IsRunning())');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_ConditionBase.c';s=p.read_text().replace('Game Mode State, Condition Group or Condition Count instead.','Game Mode State, Random Chance, Condition Group or Condition Count instead.');p.write_text(s)
