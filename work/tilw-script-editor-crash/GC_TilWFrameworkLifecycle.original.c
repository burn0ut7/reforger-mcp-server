//! Own only the subscriptions and pending work created for an active Lobby2 mission.
modded class TILW_MissionFrameworkEntity
{
	protected bool m_bGC_Lobby2Framework;
	protected SCR_BaseGameMode m_GC_ListenerGameMode;
	protected ScriptInvokerInt m_GC_StateChange;

	//------------------------------------------------------------------------------------------------
	override void EOnInit(IEntity owner)
	{
		m_bGC_Lobby2Framework = GC_LobbyGameMode.GetGameMode() != null;
		super.EOnInit(owner);
	}

	//------------------------------------------------------------------------------------------------
	override protected void InsertListeners()
	{
		if (GC_LobbyGameMode.GetGameMode())
		{
			m_bGC_Lobby2Framework = true;
			m_GC_ListenerGameMode = TILW_Lobby.GetGameMode();
			m_GC_StateChange = TILW_Lobby.GetOnGameStateChange();
		}
		super.InsertListeners();
		if (m_bGC_Lobby2Framework)
			PlayerUpdate(0, null);
	}

	//------------------------------------------------------------------------------------------------
	override protected void GameStateChange(SCR_EGameModeState state)
	{
		if (m_bGC_Lobby2Framework && state == SCR_EGameModeState.POSTGAME)
		{
			GC_CancelMissionWork();
			return;
		}
		super.GameStateChange(state);
	}

	//------------------------------------------------------------------------------------------------
	override protected void RemoveListeners()
	{
		if (!m_bGC_Lobby2Framework)
		{
			super.RemoveListeners();
			return;
		}
		if (m_GC_ListenerGameMode)
		{
			m_GC_ListenerGameMode.GetOnPlayerDeleted().Remove(PlayerUpdate);
			m_GC_ListenerGameMode.GetOnPlayerSpawned().Remove(PlayerUpdate);
		}
		if (m_GC_StateChange)
			m_GC_StateChange.Remove(GameStateChange);
		m_GC_ListenerGameMode = null;
		m_GC_StateChange = null;
		GC_CancelMissionWork();
	}

	//------------------------------------------------------------------------------------------------
	protected void GC_CancelMissionWork()
	{
		if (!GetGame())
			return;
		GetGame().GetCallqueue().Remove(InsertListeners);
		GetGame().GetCallqueue().Remove(InitRandomFlags);
		GetGame().GetCallqueue().Remove(RecheckConditions);
		GetGame().GetCallqueue().Remove(RecountPlayers);
		GetGame().GetCallqueue().Remove(RecountAI);
		if (!m_missionEvents)
			return;
		foreach (TILW_MissionEvent missionEvent : m_missionEvents)
		{
			if (!missionEvent || !missionEvent.m_instructions)
				continue;
			foreach (TILW_BaseInstruction instruction : missionEvent.m_instructions)
			{
				if (instruction)
					GetGame().GetCallqueue().Remove(instruction.Execute);
			}
		}
	}
}

modded class TILW_MissionEvent
{
	//------------------------------------------------------------------------------------------------
	override void EvalExpression(bool ignoreCondition = false, bool ignoreOccurred = false)
	{
		GC_LobbyGameMode mode = GC_LobbyGameMode.GetGameMode();
		if (mode && mode.GetState() == SCR_EGameModeState.POSTGAME)
			return;
		super.EvalExpression(ignoreCondition, ignoreOccurred);
	}
}
