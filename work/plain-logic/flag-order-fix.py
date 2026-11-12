from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text().replace('bool m_bFlagCall;', 'bool m_bFlagCall;\n\tbool m_bFlagClear;');s=s.replace('if (!CanCallFlag(name))\n\t\t\treturn false;', '''if (!m_bRunning || !m_mFlagDefinitions.Contains(name) || m_aCalledFlags.Contains(name))
			return false;
		// Earlier queued actions may make conditions pass. Check again at execution.
		if (!m_bDispatching && !CanCallFlag(name))
			return false;''',1)
needle='\tbool SetFlag(string name, bool answer)\n\t{';s=s.replace(needle, '''	bool SetFlag(string name, bool answer)
	{
		if (!m_bRunning || name.IsEmpty())
			return false;
		if (!m_mFlagDefinitions.Contains(name))
			return ApplyFlagValue(name, answer);
		if (answer)
			return CallFlag(name);
		if (m_aEvents.Count() >= 256)
		{
			StopEventLoop();
			return false;
		}
		GC_MissionRuleEvent clear = new GC_MissionRuleEvent(GC_ERuleEvent.FLAG_OFF, name);
		clear.m_bFlagClear = true;
		m_aEvents.Insert(clear);
		DrainEvents();
		return m_bRunning;
	}

	//------------------------------------------------------------------------------------------------
	protected bool ApplyFlagValue(string name, bool answer)
	{''');s=s.replace('\t\tif (answer && m_mFlagDefinitions.Contains(name))\n\t\t\treturn CallFlag(name);\n','')
s=s.replace('\t\t\tif (notification.m_bFlagCall)\n', '''			if (notification.m_bFlagClear)
			{
				ApplyFlagValue(notification.m_sName, false);
				continue;
			}
			if (notification.m_bFlagCall)
''');p.write_text(s)
p=r/'tests/McpTestAddon/scripts/Game/GC_FlagMemoryTests.c';s=p.read_text();needle='\t\tmap<string, int> counts = new map<string, int>();';s=s.replace(needle, '''		GC_CompiledMissionRule ordered = EventRule("ordered");
		ordered.m_aThen = {FlagAction("ordered_flag", true), FlagAction("ordered_flag", false)};
		GC_MissionFlag orderedFlag = Memory("ordered_flag", false);
		engine.Start({ordered}, {orderedFlag}, SCR_EGameModeState.GAME);
		engine.SendTestEvent("ordered");
		Check(!engine.GetFlag("ordered_flag") && engine.GetFlagCallCount("ordered_flag") == 1, "declared On then Off preserves action order");
		ordered.m_aThen = {FlagAction("ordered_flag", false), FlagAction("ordered_flag", true)};
		engine.Start({ordered}, {orderedFlag}, SCR_EGameModeState.GAME);
		engine.SendTestEvent("ordered");
		Check(engine.GetFlag("ordered_flag") && engine.GetFlagCallCount("ordered_flag") == 1, "declared Off then On preserves action order");
		ordered.m_aThen = {FlagAction("first", true), FlagAction("second", true)};
		engine.Start({ordered}, {first, second}, SCR_EGameModeState.GAME);
		engine.SendTestEvent("ordered");
		Check(engine.GetFlagCallCount("first") == 1 && engine.GetFlagCallCount("second") == 1, "later queued call sees earlier accepted call");

'''+needle);p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('560 checks passed (48 flag memory','563 checks passed (51 flag memory');p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace('560 checks','563 checks');p.write_text(s)
