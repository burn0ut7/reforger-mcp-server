from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text(encoding='utf-8');s=s.replace('\tref GC_CompiledMissionRule m_Rule;','\tref GC_CompiledMissionRule m_Rule;\n\tGC_MissionRuleState m_Parent;\n\tref array<ref GC_MissionRuleState> m_aChoices;',1)
a=s.index('\t\tforeach (GC_CompiledMissionRule rule : rules)');b=s.index('\t\tm_bRunning = true;',a)
s=s[:a]+'''		int total = rules.Count();
		foreach (GC_CompiledMissionRule countedRule : rules)
		{
			if (countedRule && countedRule.m_aChoices)
				total += countedRule.m_aChoices.Count();
		}
		if (total > 256)
		{
			m_iInvalidRules = 1;
			Print("GC | Mission Framework | More than 256 total events including group children; not started.", LogLevel.ERROR);
			return;
		}
		foreach (GC_CompiledMissionRule rule : rules)
		{
			GC_MissionRuleState state = CreateState(rule);
			if (!state.m_bValid)
			{
				m_iInvalidRules++;
				Print("GC | Mission Framework | Incomplete or invalid event skipped.", LogLevel.ERROR);
			}
		}
'''+s[b:]
a=s.index('\t//------------------------------------------------------------------------------------------------\n\t//! Observe');s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	protected GC_MissionRuleState CreateState(GC_CompiledMissionRule rule, GC_MissionRuleState parent = null)
	{
		GC_MissionRuleState state = new GC_MissionRuleState();
		state.m_Rule = rule;
		state.m_Parent = parent;
		state.m_bValid = rule && rule.IsValid();
		m_aStates.Insert(state);
		if (state.m_bValid)
		{
			state.m_fNextCheck = rule.m_fEverySeconds;
			if (rule.m_aChoices)
			{
				state.m_aChoices = {};
				foreach (GC_CompiledMissionRule child : rule.m_aChoices)
					state.m_aChoices.Insert(CreateState(child, state));
			}
		}
		return state;
	}

'''+s[a:]
s=s.replace('(rule.m_If && !rule.m_If.Evaluate(m_mFlags, state))','!ConditionsPass(pending)')
s=s.replace('state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_eWhen == kind','!state.m_Parent && state.m_bValid && state.m_Rule.m_bEnabled && state.m_Rule.m_eWhen == kind')
s=s.replace('if (state.m_Rule.m_eWhen == GC_ERuleEvent.TIMER','if (!state.m_Parent && state.m_Rule.m_eWhen == GC_ERuleEvent.TIMER')
s=s.replace('return !rule.m_If || rule.m_If.Evaluate(m_mFlags, m_eGameModeState);','return ConditionsPass(state);')
a=s.index('\t//------------------------------------------------------------------------------------------------\n\tprotected void TryRun');s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	protected bool ConditionsPass(GC_MissionRuleState state)
	{
		if (!state.m_bValid || !state.m_Rule.m_bEnabled)
			return false;
		if (state.m_Rule.m_If && !state.m_Rule.m_If.Evaluate(m_mFlags, m_eGameModeState))
			return false;
		return !state.m_Parent || ConditionsPass(state.m_Parent);
	}

'''+s[a:]
s=s.replace('\t\tif (!CanRun(state))\n\t\t\treturn;','\t\tif (!CanRun(state))\n\t\t\treturn;\n\t\tfloat chance = state.m_Rule.m_fChance;\n\t\tif (chance <= 0 || (chance < 1 && RandomUnit() >= chance))\n\t\t\treturn;',1)
s=s.replace('\t\t// Commit once-only state before actions can emit another event.','''		if (state.m_aChoices)
		{
			ExecuteRandomGroup(state);
			return;
		}
		// Commit once-only state before actions can emit another event.''',1)
a=s.index('\t//------------------------------------------------------------------------------------------------\n\tprotected float RandomUnit()');s=s[:a]+'''	//------------------------------------------------------------------------------------------------
	protected void ExecuteRandomGroup(GC_MissionRuleState group)
	{
		array<GC_MissionRuleState> pool = {};
		foreach (GC_MissionRuleState child : group.m_aChoices)
		{
			if (CanRun(child))
				pool.Insert(child);
		}
		// No eligible events: keep a once-only group available for a later notification.
		if (pool.IsEmpty())
			return;
		array<GC_MissionRuleState> selected = {};
		int picks = Math.Min(group.m_Rule.m_iPickCount, pool.Count());
		for (int i; i < picks; i++)
		{
			int index = Math.Min(Math.Floor(RandomUnit() * pool.Count()), pool.Count() - 1);
			selected.Insert(pool[index]);
			pool.RemoveOrdered(index);
		}
		// Commit selection once before child actions can emit another notification.
		group.m_iRuns++;
		foreach (GC_MissionRuleState chosen : selected)
			TryRun(chosen);
	}

'''+s[a:]
# Child events never receive independent trigger dispatch; only their group may select them.
needle='''			foreach (GC_MissionRuleState state : m_aStates)
			{
				if (!state.m_bValid)''';s=s.replace(needle,needle.replace('if (!state.m_bValid)','if (state.m_Parent || !state.m_bValid)'))
p.write_text(s,encoding='utf-8')
# Keep old random-action regression at the compiled interpreter level, not authored Event.
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRandomTests.c';s=p.read_text(encoding='utf-8').replace('noFlags.m_Random = new GC_RuleRandom();','GC_CompiledMissionRule compiledNoFlags = noFlags.BuildRule();\n\t\tcompiledNoFlags.m_Random = new GC_RuleRandom();').replace('noFlags.m_Random','compiledNoFlags.m_Random').replace('engine.Start({noFlags.BuildRule()});','engine.Start({compiledNoFlags});');p.write_text(s,encoding='utf-8')
# Resource migration: explicit random grouping replaces the sample's random action selector.
p=r/'Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et';p.write_text('''GC_LobbyGameMode : "{9B42B808F4E97C4D}Prefabs/MP/Modes/GameMode_Lobby.et" {
 ID "7B115D5DFF7ED998"
 components {
  GC_LobbyManager "{6A137A39106A4973}" {
   m_aMissionEvents {
    GC_RandomEventGroup "{7B115D5DFF7EDA20}" {
     m_sName "Pick one test event at match start"
     m_aConditions {
      GC_GameModeStateCondition "{7B115D5DFF7EDA21}" {
       m_eGameModeState 1
      }
     }
     m_iPickCount 1
     m_aEvents {
      GC_Event "{7B115D5DFF7EDA26}" {
       m_sName "Test A"
       m_aActions {
        GC_LogMessageAction "{7B115D5DFF7EDA22}" {
         m_sMessage "Random test A"
        }
       }
      }
      GC_Event "{7B115D5DFF7EDA27}" {
       m_sName "Test B"
       m_aActions {
        GC_LogMessageAction "{7B115D5DFF7EDA23}" {
         m_sMessage "Random test B"
        }
       }
      }
     }
     m_Advanced GC_RuleOptions "{7B115D5DFF7EDA25}" {
      m_fWaitSeconds 2
     }
    }
   }
  }
 }
}
''',encoding='utf-8')
p=r/'Prefabs/MP/Modes/GameMode_Lobby_RulesExample.et';s=p.read_text(encoding='utf-8').replace('''     m_Random GC_RuleRandom "{7B115D5DFF7EDA12}" {
     }
''','');p.write_text(s,encoding='utf-8')
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=p.read_text(encoding='utf-8').replace('GC_Event definition = GC_Event.Cast(BaseContainerTools.CreateInstanceFromContainer','GC_EventDefinition definition = GC_EventDefinition.Cast(BaseContainerTools.CreateInstanceFromContainer');s=s.replace('engine.Advance(2);\n\t\tCheck(engine.GetRunCount() == 0, "combined fixed and random delay has not elapsed");\n\t\tengine.Advance(5);','engine.Advance(1);\n\t\tCheck(engine.GetRunCount() == 0, "group fixed delay has not elapsed");\n\t\tengine.Advance(2);');p.write_text(s,encoding='utf-8')
