from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/GC_LobbyManager.c');s=p.read_text();s=s.replace('if (HasMissionRuleAuthority() && m_bMissionEventsEnabled && GetLobbyPhase() == SCR_EGameModeState.GAME\n\t\t\t&& m_MissionRuleEngine && m_MissionRuleEngine.IsRunning() && m_MissionCasualties && m_MissionCasualties.RecordDeath(victim))\n\t\t\tm_MissionRuleEngine.SetCasualties(m_MissionCasualties.GetSnapshot());\n\t\tGC_Slot slot = m_Roster.GetSlotByEntity(victim);\n\t\tif (!slot)\n\t\t\treturn;', '''bool casualtyChanged = HasMissionRuleAuthority() && m_bMissionEventsEnabled && GetLobbyPhase() == SCR_EGameModeState.GAME
			&& m_MissionRuleEngine && m_MissionRuleEngine.IsRunning() && m_MissionCasualties && m_MissionCasualties.RecordDeath(victim);
		GC_Slot slot = m_Roster.GetSlotByEntity(victim);
		if (!slot)
		{
			if (casualtyChanged)
				m_MissionRuleEngine.SetCasualties(m_MissionCasualties.GetSnapshot());
			return;
		}''');start=s.index('override void OnControllableDestroyed');end=s.index('\n\t}',start);block=s[start:end];block=block.replace('\t\tUpdateSlot(slot);','\t\tUpdateSlot(slot);\n\t\t// Finish the native death/slot flow before mission actions may change phase.\n\t\tif (casualtyChanged)\n\t\t\tm_MissionRuleEngine.SetCasualties(m_MissionCasualties.GetSnapshot());');s=s[:start]+block+s[end:];p.write_text(s)
