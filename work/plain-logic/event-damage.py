from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework')
p=r/'GC_EntityDamageObservation.c';s=p.read_text()
s=s.replace('//! One runtime observation per condition. Entity/hit-zone references are weak.\n//! Callbacks only mark changes; the authoritative framework processes them on its existing tick.', '//! One runtime observation per resolved hit zone. Entity/hit-zone references are weak.\n//! Native callbacks refresh the shared snapshot and immediately notify its owning framework.')
s=s.replace('\tprotected bool m_bChanged;', '\tprotected ref ScriptInvoker m_OnChanged = new ScriptInvoker();\n\tprotected ref array<GC_RuleCondition> m_aConditions = {};')
s=s.replace('\t\tRefresh();\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tprotected void OnHealthChanged()', '\t\tRefresh(false);\n\t}\n\n\t//------------------------------------------------------------------------------------------------\n\tprotected void OnHealthChanged()',1)
s=s.replace('protected void Refresh()', 'protected void Refresh(bool notify = true)')
s=s.replace('''		m_bAvailable = true;
		m_bDestroyed = m_HitZone.GetDamageState() == EDamageState.DESTROYED;
		m_fDamage = Math.Clamp(1 - m_HitZone.GetHealthScaled(), 0, 1);
		m_bChanged = true;''', '''		bool destroyed = m_HitZone.GetDamageState() == EDamageState.DESTROYED;
		float damage = Math.Clamp(1 - m_HitZone.GetHealthScaled(), 0, 1);
		bool changed = !m_bAvailable || destroyed != m_bDestroyed || damage != m_fDamage;
		m_bAvailable = true;
		m_bDestroyed = destroyed;
		m_fDamage = damage;
		if (notify && changed)
			m_OnChanged.Invoke(this);''')
a=s.index('\tbool ConsumeChange()');b=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tvoid Stop()',a)
s=s[:a]+'''	ScriptInvoker GetOnChanged()
	{
		return m_OnChanged;
	}

	//------------------------------------------------------------------------------------------------
	SCR_HitZone GetHitZone()
	{
		return m_HitZone;
	}

	//------------------------------------------------------------------------------------------------
	void AddCondition(GC_RuleCondition check)
	{
		if (!m_aConditions.Contains(check))
			m_aConditions.Insert(check);
	}

	//------------------------------------------------------------------------------------------------
	array<GC_RuleCondition> GetConditions()
	{
		return m_aConditions;
	}
'''+s[b:]
s=s.replace('\t\tm_bChanged = false;', '\t\tm_aConditions.Clear();')
p.write_text(s)
p=r/'GC_MissionRuleEngine.c';s=p.read_text()
needle='\tprotected int m_iRuns;'
s=s.replace(needle, '\tprotected ref map<GC_RuleCondition, bool> m_mDamageAnswers = new map<GC_RuleCondition, bool>();\n\tprotected ref map<GC_RuleCondition, bool> m_mDamageAvailability = new map<GC_RuleCondition, bool>();\n\tprotected ref array<GC_RuleCondition> m_aDamageConditions = {};\n'+needle)
s=s.replace('''		foreach (GC_RuleCondition check, GC_EntityDamageObservation observation : m_mDamage)
			observation.Stop();
		m_mDamage.Clear();''', '''		foreach (GC_RuleCondition check, GC_EntityDamageObservation observation : m_mDamage)
		{
			observation.GetOnChanged().Remove(OnEntityDamageChanged);
			observation.Stop();
		}
		m_mDamage.Clear();
		m_mDamageAnswers.Clear();
		m_mDamageAvailability.Clear();
		m_aDamageConditions.Clear();''')
# Remove recurring polling; phase bootstrap retains a one-time initial notification.
s=s.replace('\t\tQueueEntityDamageChanges();\n\t\tQueueInteractionChanges();\n\t\tforeach (GC_MissionRuleState state', '\t\tQueueInteractionChanges();\n\t\tforeach (GC_MissionRuleState state')
s=s.replace('QueueEntityDamageChanges();', 'QueueInitialEntityDamageChecks();')
a=s.index('\tprotected void RegisterEntityDamageConditions()');b=s.index('\n\t//------------------------------------------------------------------------------------------------\n\tprotected void RegisterRandomConditions()',a)
s=s[:a]+'''	protected void RegisterEntityDamageConditions()
	{
		foreach (GC_MissionRuleState state : m_aStates)
		{
			if (state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_If)
				state.m_Rule.m_If.CollectEntityDamageConditions(m_aDamageConditions);
		}
		foreach (string name, GC_MissionFlagState memory : m_mFlagDefinitions)
		{
			if (memory.m_Condition)
				memory.m_Condition.CollectEntityDamageConditions(m_aDamageConditions);
		}
		BindEntityDamageConditions();
	}

	//------------------------------------------------------------------------------------------------
	//! Startup and Match-entry resolution only. Conditions on the same native hit zone share one listener.
	protected void BindEntityDamageConditions()
	{
		foreach (GC_RuleCondition condition : m_aDamageConditions)
		{
			GC_EntityDamageObservation observation = m_mDamage.Get(condition);
			if (!observation)
				observation = new GC_EntityDamageObservation();
			observation.Bind(m_DamageWorld, condition);
			if (observation.GetHitZone())
			{
				foreach (GC_RuleCondition otherCheck, GC_EntityDamageObservation existing : m_mDamage)
				{
					if (existing != observation && existing.GetHitZone() == observation.GetHitZone())
					{
						observation.Stop();
						observation = existing;
						break;
					}
				}
			}
			observation.AddCondition(condition);
			// Remove before insert so Match-entry retries never duplicate this callback.
			observation.GetOnChanged().Remove(OnEntityDamageChanged);
			observation.GetOnChanged().Insert(OnEntityDamageChanged);
			m_mDamage.Set(condition, observation);
			RememberDamageAnswer(condition, observation);
		}
	}

	//------------------------------------------------------------------------------------------------
	protected bool RememberDamageAnswer(GC_RuleCondition check, GC_EntityDamageObservation observation)
	{
		bool available = observation.IsAvailable();
		bool answer = observation.Passes(check.m_iDamageCheck, check.m_fDamageNeeded);
		bool changed = !m_mDamageAvailability.Contains(check) || m_mDamageAvailability.Get(check) != available || m_mDamageAnswers.Get(check) != answer;
		m_mDamageAvailability.Set(check, available);
		m_mDamageAnswers.Set(check, answer);
		return changed;
	}

	//------------------------------------------------------------------------------------------------
	//! Runs on the registered native health/state callback, never on Advance or a delayed call.
	protected void OnEntityDamageChanged(GC_EntityDamageObservation observation)
	{
		if (!m_bRunning || m_eGameModeState != SCR_EGameModeState.GAME)
			return;
		GC_MissionRuleEvent notification = new GC_MissionRuleEvent(GC_ERuleEvent.ENTITY_DAMAGE_CHANGED, "");
		notification.m_aDamageChecks = {};
		foreach (GC_RuleCondition check : observation.GetConditions())
		{
			if (m_mDamage.Get(check) == observation && RememberDamageAnswer(check, observation))
				notification.m_aDamageChecks.Insert(check);
		}
		if (notification.m_aDamageChecks.IsEmpty())
			return;
		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return;
		}
		m_aEvents.Insert(notification);
		DrainEvents();
	}

	//------------------------------------------------------------------------------------------------
	//! One-time Match-entry evaluation, including conditions already true before gameplay starts.
	protected void QueueInitialEntityDamageChecks()
	{
		if (m_eGameModeState != SCR_EGameModeState.GAME || m_aDamageConditions.IsEmpty())
			return;
		GC_MissionRuleEvent notification = new GC_MissionRuleEvent(GC_ERuleEvent.ENTITY_DAMAGE_CHANGED, "");
		notification.m_aDamageChecks = {};
		notification.m_aDamageChecks.Copy(m_aDamageConditions);
		m_aEvents.Insert(notification);
	}
'''+s[b:]
p.write_text(s)
