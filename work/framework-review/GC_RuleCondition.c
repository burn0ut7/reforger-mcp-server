enum GC_ERuleCheck
{
	ALL,
	ANY,
	NONE,
	EXACTLY,
	AT_LEAST,
	AT_MOST,
	ALL_THE_SAME,
	NOT_ALL,
	// Values 8 and 9 belonged to removed XOR/XNOR modes; do not reuse them.
	LESS_THAN = 10,
	GREATER_THAN
}

//! Plain data owned by one mission; evaluation never changes flags.
[BaseContainerProps(), BaseContainerCustomTitleField("m_sName")]
class GC_RuleCondition
{
	[Attribute("Check", UIWidgets.EditBox, "A short name to help you find this check.")]
	string m_sName;

	[Attribute("0", UIWidgets.ComboBox, "Choose a group of checks, a named true/false flag, or a fixed test answer.", enums: {ParamEnum("Group of checks", "0"), ParamEnum("Flag is true", "1"), ParamEnum("Test answer", "2")})]
	int m_iCheck;

	[Attribute("0", UIWidgets.CheckBox, "Turn a passing answer into a failing answer, and a failing answer into a passing answer.")]
	bool m_bReverseAnswer;

	[Attribute("", UIWidgets.EditComboBox, "For Flag is true: the exact flag name. Missing flags are false.", enums: {ParamEnum("", "", "rst-choices:1:GC_MissionFlagChoices")})]
	string m_sFlagName;

	[Attribute("1", UIWidgets.CheckBox, "For Test answer: checked means true; unchecked means false.")]
	bool m_bTestAnswer;

	[Attribute("0", UIWidgets.ComboBox, "For a group: choose how many checks must pass. All the same means all pass or all fail.", enums: {ParamEnum("All pass", "0"), ParamEnum("Any pass", "1"), ParamEnum("None pass", "2"), ParamEnum("Exactly this many pass", "3"), ParamEnum("At least this many pass", "4"), ParamEnum("At most this many pass", "5"), ParamEnum("All give the same answer", "6")})]
	GC_ERuleCheck m_eGroupNeeds;

	[Attribute("1", UIWidgets.SpinBox, "Used by Exactly, At least, and At most. Zero is allowed.", "0 256 1")]
	int m_iHowMany;

	[Attribute("", UIWidgets.Object, "Checks inside this group. A check may itself be another group. An empty All group passes.")]
	ref array<ref GC_RuleCondition> m_aChecks;

	SCR_EGameModeState m_eGameModeState;
	int m_iFlagComparison;
	int m_iFlagCount;
	string m_sEventName;
	string m_sRandomResetName;
	string m_sTimerName;
	float m_fRandomInterval;
	float m_fTimerMaximum;
	float m_fRandomChance;
	int m_iRandomBehavior;
	ref array<string> m_aCasualtyFactions;
	int m_iCasualtyCount;
	float m_fCasualtyRatio;
	int m_iDeathComparison;
	int m_iRequiredDeaths;
	string m_sDamageEntity;
	string m_sDamageArea;
	int m_iDamageCheck;
	float m_fDamageNeeded;
	int m_iAreaTargets;
	ref array<string> m_aAreaNames;
	int m_iAreaWho;
	ref array<string> m_aAreaFactions;
	int m_iAreaComparison;
	int m_iAreaRequired;
	string m_sAreaName;
	bool m_bAreaOutside;
	bool m_bAreaAnywhere;
	bool m_bAreaProportion;
	float m_fAreaProportion;
	bool m_bLocalFactionShare;
	bool m_bShareAtMost;
	ref array<string> m_aIgnoredFactions;
	ref GC_AreaCaptureOptions m_Capture;
	ref GC_MissionScript m_Script;
	string m_sAIGroup;
	EGroupControlMode m_eAIGroupMode;
	bool m_bAIGroupContinuous;
	string m_sInteractionEntity;

	//------------------------------------------------------------------------------------------------
	//! Validate before inversion, so malformed data can never become a passing check.
	bool IsValid(int depth = 0)
	{
		if (depth > 16 || m_iCheck < 0 || m_iCheck > 14)
			return false;
		switch (m_iCheck)
		{
			case 14:
				return !m_sAIGroup.IsEmpty() && m_eAIGroupMode >= EGroupControlMode.NONE && m_eAIGroupMode < EGroupControlMode.LAST;
			case 13:
				return m_Script && m_Script.IsValid();
			case 12:
				return (GC_LobbyPhase.IsValid(m_eGameModeState)
					|| (m_eGameModeState == GC_TimerCondition.START_BY_ACTION && !m_sTimerName.IsEmpty()))
					&& m_fRandomInterval >= 0.1
					&& m_fTimerMaximum >= m_fRandomInterval && m_fTimerMaximum <= 86400;
			case 11:
				return !m_sInteractionEntity.IsEmpty();
			case 10:
				return ValidateAreaTargets();
			case 9:
				return !m_sDamageEntity.IsEmpty() && m_iDamageCheck >= 0 && m_iDamageCheck <= 2 && (m_iDamageCheck != 2 || (m_fDamageNeeded >= 0 && m_fDamageNeeded <= 1));
			case 7:
			case 8:
				if (m_iCasualtyCount < 0 || m_iCasualtyCount > 2)
					return false;
				if (m_iCheck == 7 && !(m_fCasualtyRatio >= 0 && m_fCasualtyRatio <= 1))
					return false;
				if (m_iCheck == 8 && (m_iDeathComparison < 0 || m_iDeathComparison > 3 || m_iRequiredDeaths < 0 || m_iRequiredDeaths > 1000000))
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
			case 6:
				return m_fRandomInterval >= 0.1 && m_fRandomInterval <= 86400 && m_fRandomChance >= 0 && m_fRandomChance <= 1 && m_iRandomBehavior >= 0 && m_iRandomBehavior <= 1;
			case 5:
				return !m_sEventName.IsEmpty();
			case 4:
				return !m_sFlagName.IsEmpty() && m_iFlagComparison >= 0 && m_iFlagComparison <= 2 && m_iFlagCount >= 0 && m_iFlagCount <= 1000000;
			case 3:
				return GC_LobbyPhase.IsValid(m_eGameModeState);
			case 1:
				return !m_sFlagName.IsEmpty();
			case 2:
				return true;
		}
		if (m_eGroupNeeds < 0 || m_eGroupNeeds > GC_ERuleCheck.GREATER_THAN || m_eGroupNeeds == 8 || m_eGroupNeeds == 9 || m_iHowMany < 0)
			return false;
		if (!m_aChecks)
			return true;
		if (m_aChecks.Count() > 256)
			return false;
		foreach (GC_RuleCondition check : m_aChecks)
		{
			if (!check || !check.IsValid(depth + 1))
				return false;
		}
		return true;
	}

	//------------------------------------------------------------------------------------------------
	bool UsesGameModeState(int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 3 || m_iCheck == 12)
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

	//------------------------------------------------------------------------------------------------
	bool UsesFlag(string name = "", int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 1 || m_iCheck == 4)
			return name.IsEmpty() || m_sFlagName == name;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesFlag(name, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	bool UsesEvent(string name = "", int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 5)
			return name.IsEmpty() || m_sEventName == name;
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesEvent(name, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	protected bool ValidateAreaTargets()
	{
		if ((!m_bAreaAnywhere && m_sAreaName.IsEmpty()) || m_iAreaTargets < 0 || m_iAreaTargets > 1 || m_iAreaComparison < 0 || m_iAreaComparison > 3 || m_iAreaRequired < 0 || m_iAreaRequired > 4096)
			return false;
		if (m_bAreaAnywhere && (m_iAreaTargets != 1 || !m_sAreaName.IsEmpty() || m_bAreaOutside))
			return false;
		if (m_bAreaProportion && (m_bAreaAnywhere || !(m_fAreaProportion >= 0 && m_fAreaProportion <= 1)))
			return false;
		if (m_Capture && (m_bAreaAnywhere || !m_Capture.IsValid()))
			return false;
		if (m_bLocalFactionShare)
		{
			if (m_iAreaTargets != 1 || m_bAreaAnywhere || m_bAreaProportion || !m_aAreaFactions || m_aAreaFactions.IsEmpty()
				|| !(m_fAreaProportion >= 0 && m_fAreaProportion <= 1))
				return false;
			if (m_aIgnoredFactions)
			{
				if (m_aIgnoredFactions.Count() > 256)
					return false;
				foreach (string ignored : m_aIgnoredFactions)
				{
					if (ignored.IsEmpty() || m_aAreaFactions.Contains(ignored))
						return false;
				}
			}
		}
		array<string> keys = m_aAreaNames;
		if (m_iAreaTargets == 1)
		{
			if (m_iAreaWho < 0 || m_iAreaWho > 2)
				return false;
			keys = m_aAreaFactions;
		}
		else if (!keys || keys.IsEmpty())
			return false;
		if (keys)
		{
			if (keys.Count() > 256)
				return false;
			foreach (string key : keys)
			{
				if (key.IsEmpty())
					return false;
			}
		}
		return true;
	}

	//------------------------------------------------------------------------------------------------
	bool UsesArea(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 10)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesArea(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectAreaConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 10 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectAreaConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	bool UsesAIGroup(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 14)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesAIGroup(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectAIGroupConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 14 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectAIGroupConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	bool UsesScript(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 13)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesScript(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectScriptConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 13 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectScriptConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	bool UsesInteraction(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 11)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesInteraction(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectInteractionConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 11 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectInteractionConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	bool UsesEntityDamage(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 9)
			return !changed || changed.Contains(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child && child.UsesEntityDamage(changed, depth + 1))
					return true;
			}
		}
		return false;
	}

	//------------------------------------------------------------------------------------------------
	void CollectEntityDamageConditions(array<GC_RuleCondition> result, int depth = 0)
	{
		if (depth > 16)
			return;
		if (m_iCheck == 9 && !result.Contains(this))
			result.Insert(this);
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (child)
					child.CollectEntityDamageConditions(result, depth + 1);
			}
		}
	}

	//------------------------------------------------------------------------------------------------
	bool UsesCasualties(int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 7 || m_iCheck == 8)
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

	//------------------------------------------------------------------------------------------------
	bool UsesRandom(array<GC_RuleCondition> changed = null, int depth = 0)
	{
		if (depth > 16)
			return false;
		if (m_iCheck == 6 || m_iCheck == 12)
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
		if ((m_iCheck == 6 || m_iCheck == 12) && !result.Contains(this))
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
	protected bool HasEvaluationContext(map<string, bool> events, map<GC_RuleCondition, bool> randomResults, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage, GC_MissionAreaTracker areas, map<GC_RuleCondition, ref GC_InteractionObservation> interactions, map<GC_RuleCondition, bool> scriptResults, map<GC_RuleCondition, ref GC_AIGroupObservation> aiGroups, int depth = 0)
	{
		if (depth > 16)
			return false;
		switch (m_iCheck)
		{
			case 14:
				GC_AIGroupObservation group;
				return aiGroups && aiGroups.Find(this, group) && group && group.IsAvailable(this);
			case 13:
				return scriptResults && scriptResults.Contains(this);
			case 11:
				GC_InteractionObservation interaction;
				return interactions && interactions.Find(this, interaction) && interaction && interaction.IsAvailable();
			case 10:
				return areas && areas.IsAvailable(this);
			case 9:
				GC_EntityDamageObservation observation;
				return damage && damage.Find(this, observation) && observation && observation.IsAvailable();
			case 6:
			case 12:
				return randomResults && randomResults.Contains(this);
			case 5:
				return events && events.Contains(m_sEventName);
		}
		if (m_iCheck == 0 && m_aChecks)
		{
			foreach (GC_RuleCondition child : m_aChecks)
			{
				if (!child || !child.HasEvaluationContext(events, randomResults, damage, areas, interactions, scriptResults, aiGroups, depth + 1))
					return false;
			}
		}
		return true;
	}

	//------------------------------------------------------------------------------------------------
	bool Evaluate(map<string, bool> flags, SCR_EGameModeState gameModeState = -1, map<string, int> counts = null, map<string, bool> events = null, map<GC_RuleCondition, bool> randomResults = null, GC_CasualtySnapshot casualties = null, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage = null, GC_MissionAreaTracker areas = null, map<GC_RuleCondition, ref GC_InteractionObservation> interactions = null, map<GC_RuleCondition, bool> scriptResults = null, map<GC_RuleCondition, ref GC_AIGroupObservation> aiGroups = null)
	{
		if (!IsValid() || !HasEvaluationContext(events, randomResults, damage, areas, interactions, scriptResults, aiGroups) || (UsesGameModeState() && !GC_LobbyPhase.IsValid(gameModeState)))
			return false;
		return EvaluateValid(flags, gameModeState, counts, events, randomResults, casualties, damage, areas, interactions, scriptResults, aiGroups);
	}

	//------------------------------------------------------------------------------------------------
	protected bool EvaluateValid(map<string, bool> flags, SCR_EGameModeState gameModeState, map<string, int> counts, map<string, bool> events, map<GC_RuleCondition, bool> randomResults, GC_CasualtySnapshot casualties, map<GC_RuleCondition, ref GC_EntityDamageObservation> damage, GC_MissionAreaTracker areas, map<GC_RuleCondition, ref GC_InteractionObservation> interactions, map<GC_RuleCondition, bool> scriptResults, map<GC_RuleCondition, ref GC_AIGroupObservation> aiGroups)
	{
		bool answer;
		switch (m_iCheck)
		{
			case 1:
				if (flags)
					flags.Find(m_sFlagName, answer);
				break;
			case 14:
				answer = aiGroups.Get(this).Passes(this);
				break;
			case 13:
				scriptResults.Find(this, answer);
				break;
			case 11:
				answer = interactions.Get(this).IsTriggered();
				break;
			case 10:
				answer = areas.PassesCondition(this);
				break;
			case 9:
				answer = damage.Get(this).Passes(m_iDamageCheck, m_fDamageNeeded);
				break;
			case 8:
				answer = casualties && casualties.PassesDeathCount(m_aCasualtyFactions, m_iCasualtyCount, m_iDeathComparison, m_iRequiredDeaths);
				break;
			case 7:
				answer = casualties && casualties.Passes(m_aCasualtyFactions, m_iCasualtyCount, m_fCasualtyRatio);
				break;
			case 6:
			case 12:
				randomResults.Find(this, answer);
				break;
			case 5:
				events.Find(m_sEventName, answer);
				break;
			case 4:
				int calls;
				if (counts)
					counts.Find(m_sFlagName, calls);
				if (m_iFlagComparison == 0)
					answer = calls > m_iFlagCount;
				else if (m_iFlagComparison == 1)
					answer = calls < m_iFlagCount;
				else
					answer = calls == m_iFlagCount;
				break;
			case 3:
				answer = gameModeState == m_eGameModeState;
				break;
			case 2:
				answer = m_bTestAnswer;
				break;
			default:
				int passed;
				int count;
				if (m_aChecks)
				{
					count = m_aChecks.Count();
					foreach (GC_RuleCondition check : m_aChecks)
					{
						if (check.EvaluateValid(flags, gameModeState, counts, events, randomResults, casualties, damage, areas, interactions, scriptResults, aiGroups))
							passed++;
					}
				}
				switch (m_eGroupNeeds)
				{
					case GC_ERuleCheck.ALL:
						answer = passed == count;
						break;
					case GC_ERuleCheck.ANY:
						answer = passed > 0;
						break;
					case GC_ERuleCheck.NONE:
						answer = passed == 0;
						break;
					case GC_ERuleCheck.EXACTLY:
						answer = passed == m_iHowMany;
						break;
					case GC_ERuleCheck.AT_LEAST:
						answer = passed >= m_iHowMany;
						break;
					case GC_ERuleCheck.AT_MOST:
						answer = passed <= m_iHowMany;
						break;
					case GC_ERuleCheck.ALL_THE_SAME:
						answer = passed == 0 || passed == count;
						break;
					case GC_ERuleCheck.NOT_ALL:
						answer = passed < count;
						break;
					case GC_ERuleCheck.LESS_THAN:
						answer = passed < m_iHowMany;
						break;
					case GC_ERuleCheck.GREATER_THAN:
						answer = passed > m_iHowMany;
						break;
				}
				break;
		}
		if (m_bReverseAnswer)
			return !answer;
		return answer;
	}
}
