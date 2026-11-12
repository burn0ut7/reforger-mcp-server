from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
f=r/'scripts/Game/MissionFramework'
p=f/'GC_RuleCondition.c';s=p.read_text()
s=s.replace('\tfloat m_fAreaProportion;', '\tfloat m_fAreaProportion;\n\tstring m_sInteractionEntity;')
s=s.replace('m_iCheck > 10)', 'm_iCheck > 11)')
s=s.replace('\t\tif (m_iCheck == 10)\n\t\t\treturn ValidateAreaTargets();','\t\tif (m_iCheck == 11)\n\t\t\treturn !m_sInteractionEntity.IsEmpty();\n\t\tif (m_iCheck == 10)\n\t\t\treturn ValidateAreaTargets();')
a=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tbool UsesEntityDamage')
block=s[a:s.index('\n\t//------------------------------------------------------------------------------------------------\n\tbool UsesCasualties',a)]
block=block.replace('UsesEntityDamage','UsesInteraction').replace('CollectEntityDamageConditions','CollectInteractionConditions').replace('m_iCheck == 9','m_iCheck == 11')
s=s[:a]+block+s[a:]
s=s.replace('GC_MissionAreaTracker areas, int depth = 0)', 'GC_MissionAreaTracker areas, map<GC_RuleCondition, ref GC_InteractionObservation> interactions, int depth = 0)')
s=s.replace('GC_MissionAreaTracker areas = null)', 'GC_MissionAreaTracker areas = null, map<GC_RuleCondition, ref GC_InteractionObservation> interactions = null)')
s=s.replace('GC_MissionAreaTracker areas)', 'GC_MissionAreaTracker areas, map<GC_RuleCondition, ref GC_InteractionObservation> interactions)')
s=s.replace('damage, areas, depth + 1)', 'damage, areas, interactions, depth + 1)').replace('damage, areas)', 'damage, areas, interactions)')
a=s.index('\t\tif (m_iCheck == 10)',s.index('protected bool HasEvaluationContext'))
s=s[:a]+'''		if (m_iCheck == 11)
		{
			GC_InteractionObservation interaction;
			return interactions && interactions.Find(this, interaction) && interaction && interaction.IsAvailable();
		}
'''+s[a:]
s=s.replace('\t\telse if (m_iCheck == 10)', '\t\telse if (m_iCheck == 11)\n\t\t\tanswer = interactions.Get(this).HasBeenUsed();\n\t\telse if (m_iCheck == 10)')
p.write_text(s)
p=f/'GC_CompiledMissionRule.c';s=p.read_text().replace('\tAREA_CHANGED\n','\tAREA_CHANGED,\n\tINTERACTION_USED\n');p.write_text(s)
p=f/'GC_MissionRuleEngine.c';s=p.read_text()
s=s.replace('\tref array<GC_RuleCondition> m_aAreaChecks;', '\tref array<GC_RuleCondition> m_aAreaChecks;\n\tref array<GC_RuleCondition> m_aInteractionChecks;')
s=s.replace('\tprotected int m_iRuns;', '\tref ScriptInvoker m_OnStateChanged = new ScriptInvoker();\n\tprotected ref map<GC_RuleCondition, ref GC_InteractionObservation> m_mInteractions = new map<GC_RuleCondition, ref GC_InteractionObservation>();\n\tprotected int m_iRuns;')
s=s.replace('RegisterEntityDamageConditions();\n', 'RegisterEntityDamageConditions();\n\t\tRegisterInteractionConditions();\n',1)
s=s.replace('m_Casualties, m_mDamage, m_Areas)', 'm_Casualties, m_mDamage, m_Areas, m_mInteractions)')
s=s.replace('state.m_Rule.m_If.UsesArea()', 'state.m_Rule.m_If.UsesArea() || state.m_Rule.m_If.UsesInteraction()')
s=s.replace('\t\t\tBindEntityDamageConditions();', '\t\t{\n\t\t\tBindEntityDamageConditions();\n\t\t\tBindInteractionConditions();\n\t\t}')
s=s.replace('\t\tQueueEntityDamageChanges();', '\t\tQueueEntityDamageChanges();\n\t\tQueueInteractionChanges();')
s=s.replace('\t\tm_mDamage.Clear();', '\t\tm_mDamage.Clear();\n\t\tforeach (GC_RuleCondition interactionCheck, GC_InteractionObservation interaction : m_mInteractions)\n\t\t\tinteraction.Stop();\n\t\tm_mInteractions.Clear();')
s=s.replace('\t\t\tstate.m_bPending = false;\n\t}\n', '\t\t\tstate.m_bPending = false;\n\t\tm_OnStateChanged.Invoke();\n\t}\n',1)
a=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tprotected void RegisterEntityDamageConditions')
b=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tprotected void RegisterRandomConditions',a)
block=s[a:b].replace('EntityDamage','Interaction').replace('m_mDamage','m_mInteractions').replace('GC_EntityDamageObservation','GC_InteractionObservation').replace('ENTITY_DAMAGE_CHANGED','INTERACTION_USED').replace('m_aDamageChecks','m_aInteractionChecks').replace('//! Coalesce health/state callbacks so the complete tree sees the latest settled damage values.', '//! Coalesce component notifications in the existing framework queue.')
s=s[:a]+block+s[a:]
a=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tprotected void DrainEvents()')
s=s[:a]+'''
	//------------------------------------------------------------------------------------------------
	//! Interaction observations update before the optional flag call; dispatch shares one chain.
	void NotifyInteraction(string callFlag)
	{
		if (!m_bRunning || m_eGameModeState != SCR_EGameModeState.GAME)
			return;
		QueueInteractionChanges();
		if (!callFlag.IsEmpty())
			CallFlag(callFlag);
		DrainEvents();
	}
'''+s[a:]
s=s.replace('\t\tm_bDispatching = true;\n\t\tint processed;', '\t\tbool changed = !m_aEvents.IsEmpty();\n\t\tm_bDispatching = true;\n\t\tint processed;')
s=s.replace('\t\t\tbool areaChanged =', '\t\t\tbool interactionUsed = notification.m_eKind == GC_ERuleEvent.INTERACTION_USED;\n\t\t\tbool areaChanged =')
s=s.replace('|| (areaChanged && memory.m_Condition.UsesArea(notification.m_aAreaChecks))', '|| (areaChanged && memory.m_Condition.UsesArea(notification.m_aAreaChecks))\n\t\t\t\t\t|| (interactionUsed && memory.m_Condition.UsesInteraction(notification.m_aInteractionChecks))')
s=s.replace('|| (areaChanged && rule.m_If.UsesArea(notification.m_aAreaChecks))))', '|| (areaChanged && rule.m_If.UsesArea(notification.m_aAreaChecks))\n\t\t\t\t\t|| (interactionUsed && rule.m_If.UsesInteraction(notification.m_aInteractionChecks))))')
s=s.replace('\t\tResetPulses();\n\t\tm_bDispatching = false;', '\t\tResetPulses();\n\t\tm_bDispatching = false;\n\t\tif (changed)\n\t\t\tm_OnStateChanged.Invoke();')
p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text()
a=s.index('\tprotected bool HasMissionRuleAuthority()')
s=s[:a]+'''	protected static ref ScriptInvoker s_MissionStateChanged = new ScriptInvoker();

	//------------------------------------------------------------------------------------------------
	static ScriptInvoker GetMissionStateChanged()
	{
		return s_MissionStateChanged;
	}

	//------------------------------------------------------------------------------------------------
	protected void PublishMissionStateChanged()
	{
		s_MissionStateChanged.Invoke();
	}

	//------------------------------------------------------------------------------------------------
	bool AreMissionInteractionsActive()
	{
		return HasMissionRuleAuthority() && m_bMissionEventsEnabled && GetLobbyPhase() == SCR_EGameModeState.GAME
			&& m_MissionRuleEngine && m_MissionRuleEngine.IsRunning();
	}

	//------------------------------------------------------------------------------------------------
	void NotifyMissionInteraction(string callFlag)
	{
		if (AreMissionInteractionsActive())
			m_MissionRuleEngine.NotifyInteraction(callFlag);
	}

	//------------------------------------------------------------------------------------------------
'''+s[a:]
s=s.replace('m_MissionRuleEngine = new GC_MissionRuleEngine();','m_MissionRuleEngine = new GC_MissionRuleEngine();\n\t\tm_MissionRuleEngine.m_OnStateChanged.Insert(PublishMissionStateChanged);')
s=s.replace('\t\t\tm_MissionRuleEngine.Stop();', '\t\t{\n\t\t\tm_MissionRuleEngine.Stop();\n\t\t\tm_MissionRuleEngine.m_OnStateChanged.Remove(PublishMissionStateChanged);\n\t\t}')
p.write_text(s)
