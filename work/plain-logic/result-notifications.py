from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text();needle='\t\tm_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.EVENT_RAN, name));';s=s.replace(needle,'''\t\tbool watched;
		foreach (GC_MissionRuleState observer : m_aStates)
		{
			if (!observer.m_Parent && observer.m_bValid && observer.m_Rule.m_bObserveEvents && observer.m_Rule.m_If && observer.m_Rule.m_If.UsesEvent(name))
				watched = true;
		}
		foreach (string flagName, GC_MissionFlagState memory : m_mFlagDefinitions)
		{
			if (memory.m_Condition && memory.m_Condition.UsesEvent(name))
				watched = true;
		}
		if (watched)
			m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.EVENT_RAN, name));''');# Only impose notification queue limit if a notification is needed
old='''		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return;
		}
		bool watched;''';s=s.replace(old,'\t\tbool watched;',1).replace('''		if (watched)
			m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.EVENT_RAN, name));''','''		if (!watched)
			return;
		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return;
		}
		m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.EVENT_RAN, name));''');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=p.read_text().replace('m_eWhen > GC_ERuleEvent.EVENT_RAN','m_eWhen > GC_ERuleEvent.GAME_MODE_STATE');p.write_text(s)
