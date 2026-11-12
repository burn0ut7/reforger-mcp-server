from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
def read(f): return (r/f).read_text(encoding='utf-8')
def write(f,s): (r/f).write_text(s,encoding='utf-8')
f='scripts/Game/MissionFramework/GC_GameModeStateCondition.c'
write(f,'''//! Reads the active Lobby2 state supplied by its authoritative game-mode callback.
[BaseContainerProps(), BaseContainerCustomStringTitleField("Game Mode State")]
class GC_GameModeStateCondition : GC_MissionCondition
{
	[Attribute("1", UIWidgets.ComboBox, "Choose the mission phase. Match means active play after briefing.", enums: {ParamEnum("Mission preview", "0"), ParamEnum("Role selection", "1787356801"), ParamEnum("Briefing", "1787356802"), ParamEnum("Match", "1"), ParamEnum("After action", "2")})]
	SCR_EGameModeState m_eGameModeState;

	[Attribute("0", UIWidgets.ComboBox, "Is in passes while the selected phase is active. Is not in passes in other phases. Checked when the framework starts and whenever the state changes, not repeatedly each frame.", enums: {ParamEnum("Is in this state", "0"), ParamEnum("Is not in this state", "1")})]
	int m_iMatch;

	//------------------------------------------------------------------------------------------------
	override GC_RuleCondition BuildCheck(int depth = 0)
	{
		if (depth > 16 || !GC_LobbyPhase.IsValid(m_eGameModeState) || m_iMatch < 0 || m_iMatch > 1)
			return null;
		GC_RuleCondition check = new GC_RuleCondition();
		check.m_iCheck = 3;
		check.m_eGameModeState = m_eGameModeState;
		check.m_bReverseAnswer = m_iMatch == 1;
		return check;
	}
}
''')
f='scripts/Game/MissionFramework/GC_RuleCondition.c';s=read(f).replace('m_iCheck > 2','m_iCheck > 3').replace('\t\tif (m_iCheck == 1)\n', '\t\tif (m_iCheck == 3)\n\t\t\treturn GC_LobbyPhase.IsValid(m_eGameModeState);\n\t\tif (m_iCheck == 1)\n',1)
s=s.replace('\t//------------------------------------------------------------------------------------------------\n\t//! Validate before inversion','\tSCR_EGameModeState m_eGameModeState;\n\n\t//------------------------------------------------------------------------------------------------\n\t//! Validate before inversion')
s=s.replace('bool Evaluate(map<string, bool> flags)','bool Evaluate(map<string, bool> flags, SCR_EGameModeState gameModeState = -1)').replace('if (!IsValid())','if (!IsValid() || (UsesGameModeState() && !GC_LobbyPhase.IsValid(gameModeState)))').replace('return EvaluateValid(flags);','return EvaluateValid(flags, gameModeState);').replace('protected bool EvaluateValid(map<string, bool> flags)','protected bool EvaluateValid(map<string, bool> flags, SCR_EGameModeState gameModeState)').replace('check.EvaluateValid(flags)','check.EvaluateValid(flags, gameModeState)').replace('\t\telse if (m_iCheck == 2)','\t\telse if (m_iCheck == 3)\n\t\t\tanswer = gameModeState == m_eGameModeState;\n\t\telse if (m_iCheck == 2)')
a=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool Evaluate(')
s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	bool UsesGameModeState(int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 3)
			return true;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesGameModeState(depth + 1))
					return true;
			}
		}
		return false;
	}

'''+s[a:];write(f,s)
f='scripts/Game/MissionFramework/GC_CompiledMissionRule.c';s=read(f).replace('\tFLAG_OFF\n','\tFLAG_OFF,\n\tGAME_MODE_STATE\n').replace('m_eWhen > GC_ERuleEvent.FLAG_OFF','m_eWhen > GC_ERuleEvent.GAME_MODE_STATE').replace('m_eWhen >= GC_ERuleEvent.TEST_EVENT && m_sTestEventName.IsEmpty()','m_eWhen >= GC_ERuleEvent.TEST_EVENT && m_eWhen <= GC_ERuleEvent.FLAG_OFF && m_sTestEventName.IsEmpty()');write(f,s)
f='scripts/Game/MissionFramework/GC_MissionRule.c';s=read(f);a=s.index('\t[Attribute("", UIWidgets.Object, "The event');b=s.index('\t[Attribute("", UIWidgets.Object, "The actions',a);s=s[:a]+s[b:];s=s.replace('\t\tif (m_Event && !m_Event.ApplyTo(rule))\n\t\t\treturn null;\n','');s=s.replace('\t\t\tif (!rule.m_If)\n\t\t\t\treturn null;','\t\t\tif (!rule.m_If)\n\t\t\t\treturn null;\n\t\t\tif (rule.m_If.UsesGameModeState())\n\t\t\t\trule.m_eWhen = GC_ERuleEvent.GAME_MODE_STATE;');write(f,s)
(r/'scripts/Game/MissionFramework/GC_MissionEvent.c').unlink()
f='scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=read(f).replace('\tprotected bool m_bRunning;','\tprotected SCR_EGameModeState m_eGameModeState = -1;\n\tprotected bool m_bRunning;').replace('array<ref GC_MissionFlag> flags = null)','array<ref GC_MissionFlag> flags = null, SCR_EGameModeState initialState = -1)').replace('\t\tm_fNow = 0;','\t\tm_fNow = 0;\n\t\tm_eGameModeState = -1;',1)
s=s.replace('\t\tm_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.MATCH_BEGINS, ""));\n\t\tDrainEvents();','''		if (GC_LobbyPhase.IsValid(initialState))
			SetGameModeState(initialState);
		else
		{
			// Historical interpreter callers without a live state retain match-start semantics.
			m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.MATCH_BEGINS, ""));
			DrainEvents();
		}''',1)
a=s.index('\t//------------------------------------------------------------------------------------------------\n\tvoid Stop()');s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	//! Observe the owning mode; this never changes the engine's authoritative phase.
	void SetGameModeState(SCR_EGameModeState state, float elapsedSeconds = -1)
	{
		if (!m_bRunning || !GC_LobbyPhase.IsValid(state) || state == m_eGameModeState)
			return;
		if (elapsedSeconds > m_fNow)
			m_fNow = elapsedSeconds;
		m_eGameModeState = state;
		foreach (GC_MissionRuleState pending : m_aStates)
		{
			if (!pending.m_bValid || !pending.m_bPending)
				continue;
			GC_CompiledMissionRule rule = pending.m_Rule;
			if ((rule.m_If && !rule.m_If.Evaluate(m_mFlags, state))
				|| (state == SCR_EGameModeState.POSTGAME && rule.m_eWhen != GC_ERuleEvent.GAME_MODE_STATE))
				pending.m_bPending = false;
		}
		m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.GAME_MODE_STATE, ""));
		if (state == SCR_EGameModeState.GAME)
			m_aEvents.Insert(new GC_MissionRuleEvent(GC_ERuleEvent.MATCH_BEGINS, ""));
		DrainEvents();
	}

'''+s[a:]
s=s.replace('rule.m_If.Evaluate(m_mFlags)','rule.m_If.Evaluate(m_mFlags, m_eGameModeState)').replace('state.m_Rule.m_eWhen == GC_ERuleEvent.TIMER &&','state.m_Rule.m_eWhen == GC_ERuleEvent.TIMER && m_eGameModeState != SCR_EGameModeState.POSTGAME &&').replace('notification.m_eKind == GC_ERuleEvent.MATCH_BEGINS\n','notification.m_eKind == GC_ERuleEvent.MATCH_BEGINS || notification.m_eKind == GC_ERuleEvent.GAME_MODE_STATE\n');write(f,s)
f='scripts/Game/GC_LobbyManager.c';s=read(f).replace('mode == GetOwner() && mode.GetState() == SCR_EGameModeState.GAME','mode == GetOwner() && GC_LobbyPhase.IsValid(mode.GetState())',1).replace('override void OnGameModeStart()\n\t{\n\t\tsuper.OnGameModeStart();','protected void StartMissionRules()\n\t{',1).replace('m_MissionRuleEngine.Start(runtimeRules, m_aMissionFlags);','m_MissionRuleEngine.Start(runtimeRules, m_aMissionFlags, GetLobbyPhase());',1).replace('StartPhaseClock(GetLobbyPhase());','StartPhaseClock(GetLobbyPhase());\n\t\t\tStartMissionRules();',1)
s=s.replace('\t\tif (state == SCR_EGameModeState.POSTGAME)\n\t\t\tStopMissionRules();','\t\tUpdateMissionGameState(state);',1)
a=s.index('\t//------------------------------------------------------------------------------------------------\n\tprotected void TickMissionRules()');s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	protected void UpdateMissionGameState(SCR_EGameModeState state)
	{
		if (!HasMissionRuleAuthority() || !m_bMissionEventsEnabled || state != GetLobbyPhase())
			return;
		StartMissionRules();
		if (m_MissionRuleEngine)
			m_MissionRuleEngine.SetGameModeState(state, (GetGame().GetWorld().GetWorldTime() - m_fMissionRulesStartedAt) * 0.001);
	}

'''+s[a:];write(f,s)
