class GC_PerformanceRuleEngineProbe : GC_MissionRuleEngine
{
	ref GC_RuleCondition m_TestAnswer;

	void Prepare(GC_LobbyManager manager)
	{
		m_TestAnswer = new GC_RuleCondition();
		m_TestAnswer.m_iCheck = 2;
		m_TestAnswer.m_bTestAnswer = true;
		GC_RuleAction log = new GC_RuleAction();
		log.m_eDo = GC_ERuleAction.WRITE_LOG;
		log.m_sTextOrName = "performance completion check";
		GC_CompiledMissionRule delayed = new GC_CompiledMissionRule();
		delayed.m_sName = "delayed";
		delayed.m_bEnabled = true;
		delayed.m_bRunOnlyOnce = true;
		delayed.m_eWhen = GC_ERuleEvent.TEST_EVENT;
		delayed.m_sTestEventName = "wait";
		delayed.m_fWaitSeconds = 0.1;
		delayed.m_If = m_TestAnswer;
		delayed.m_aThen = {log};
		GC_CompiledMissionRule timer = new GC_CompiledMissionRule();
		timer.m_sName = "timer";
		timer.m_bEnabled = true;
		timer.m_bRunOnlyOnce = true;
		timer.m_eWhen = GC_ERuleEvent.TIMER;
		timer.m_fEverySeconds = 0.1;
		timer.m_aThen = {log};
		Start({delayed, timer}, null, SCR_EGameModeState.GAME, null, null, null, manager);
	}

	bool CheckSuspended()
	{
		return m_bWaitingForRespawnWave && m_aStates[0].m_bPending && GetRunCount() == 0 && m_fNow == 1;
	}
}

modded class GC_LobbyManager
{
	protected ref GC_PerformanceRuleEngineProbe m_GC_PerformanceEngine;
	protected bool m_bGC_MeasureStart;
	protected int m_iGC_TestSpawnCallbacks;
	protected int m_iGC_TestMaximumSpawns;
	protected int m_iGC_TestExpectedSpawns;

	string GC_TestWaveOrdering()
	{
		GC_LobbyManager manager = GC_LobbyManager.Cast(this);
		if (!Replication.IsServer() || GetLobbyPhase() != SCR_EGameModeState.GAME || manager.GC_IsRespawnWavePending())
			return "BLOCKED: idle authority match required";
		StopMissionRules();
		m_bMissionEventsEnabled = true;
		m_GC_PerformanceEngine = new GC_PerformanceRuleEngineProbe();
		m_MissionRuleEngine = m_GC_PerformanceEngine;
		m_GC_PerformanceEngine.Prepare(manager);
		m_GC_PerformanceEngine.SendTestEvent("wait");
		for (int index; index < 16; index++)
		{
			if (!manager.GC_QueueRespawnWave({}, 0, false))
				return "FAIL: prefix wave rejected";
		}
		GC_RuleAction wave = new GC_RuleAction();
		wave.m_eDo = GC_ERuleAction.RESPAWN_WAVE;
		wave.m_aFlagNames = {};
		if (!m_GC_PerformanceEngine.QueueFrameworkAction(wave))
			return "FAIL: framework wave rejected";
		m_GC_PerformanceEngine.m_TestAnswer.m_bTestAnswer = false;
		m_GC_PerformanceEngine.Advance(1);
		bool suspended = m_GC_PerformanceEngine.CheckSuspended();
		m_GC_PerformanceEngine.m_TestAnswer.m_bTestAnswer = true;
		return string.Format("queued suspended=%1; call GC_CheckWaveOrdering", suspended);
	}

	string GC_CheckWaveOrdering()
	{
		if (GC_LobbyManager.Cast(this).GC_IsRespawnWavePending())
			return "PENDING";
		if (!m_GC_PerformanceEngine)
			return "BLOCKED: no ordering fixture";
		m_GC_PerformanceEngine.Advance(1);
		return string.Format("passed=%1 runs=%2", m_GC_PerformanceEngine.GetRunCount() == 2, m_GC_PerformanceEngine.GetRunCount());
	}

	protected int GC_CountTestBodies()
	{
		int count;
		foreach (GC_Slot slot : GetSlots())
		{
			if (slot.GetEntity())
				count++;
		}
		return count;
	}

	override protected void ProcessBriefingSlotSpawns()
	{
		int before = GC_CountTestBodies();
		super.ProcessBriefingSlotSpawns();
		if (m_bGC_MeasureStart)
		{
			m_iGC_TestSpawnCallbacks++;
			m_iGC_TestMaximumSpawns = Math.Max(m_iGC_TestMaximumSpawns, GC_CountTestBodies() - before);
		}
	}

	string GC_TestBatchedStart()
	{
		if (!Replication.IsServer() || GetLobbyPhase() == SCR_EGameModeState.GAME)
			return "BLOCKED: fresh pre-match Play required";
		GC_LobbyGameMode.GetGameMode().GC_SetLobbyState(SCR_EGameModeState.SLOTTING);
		foreach (GC_Slot slot : GetSlots())
		{
			if (m_iGC_TestExpectedSpawns >= 16)
				break;
			if (!slot.HasClaim() && !slot.GetEntity())
			{
				slot.ReserveIdentity(PersistenceIdUtils.Generate(), "Performance fixture");
				m_iGC_TestExpectedSpawns++;
			}
		}
		if (m_iGC_TestExpectedSpawns < 5)
			return "BLOCKED: five empty authored slots required";
		GC_LobbyGameMode.GetGameMode().GC_SetLobbyState(SCR_EGameModeState.BRIEFING);
		m_bGC_MeasureStart = true;
		int before = GC_CountTestBodies();
		bool first = StartLobbyGame();
		bool duplicate = StartLobbyGame();
		return string.Format("queued=%1 duplicateRejected=%2 synchronousSpawns=%3 expected=%4", first, !duplicate, GC_CountTestBodies() - before, m_iGC_TestExpectedSpawns);
	}

	string GC_CheckBatchedStart()
	{
		if (m_bStartGamePending)
			return "PENDING";
		bool passed = GetLobbyPhase() == SCR_EGameModeState.GAME && m_iGC_TestSpawnCallbacks > 1
			&& m_iGC_TestMaximumSpawns <= 4 && GC_CountTestBodies() >= m_iGC_TestExpectedSpawns;
		return string.Format("passed=%1 callbacks=%2 maxSpawnsPerCallback=%3 bodies=%4", passed, m_iGC_TestSpawnCallbacks, m_iGC_TestMaximumSpawns, GC_CountTestBodies());
	}
}
