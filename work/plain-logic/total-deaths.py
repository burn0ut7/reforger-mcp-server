from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_CasualtiesCondition.c';s=p.read_text().replace('int m_iDeaths;','int m_iDeaths;\n\tint m_iTotalDeaths;\n\n\t//------------------------------------------------------------------------------------------------\n\tbool Matches(array<string> factions, int selection)\n\t{\n\t\treturn (!factions || factions.IsEmpty() || factions.Contains(m_sFaction))\n\t\t\t&& (selection == 2 || (selection == 0 && m_bPlayers) || (selection == 1 && !m_bPlayers));\n\t}');s=s.replace('GC_CasualtyTotals AddStartingLife(string faction, bool player)','protected GC_CasualtyTotals GetTotals(string faction, bool player)');s=s.replace('''			{
				totals.m_iStarting++;
				return totals;
			}''','\t\t\t\treturn totals;',1);s=s.replace('\n\t\tcreated.m_iStarting = 1;', '');pos=s.index('\t//------------------------------------------------------------------------------------------------\n\tbool Passes(');s=s[:pos]+'''	//------------------------------------------------------------------------------------------------
	GC_CasualtyTotals AddStartingLife(string faction, bool player)
	{
		GC_CasualtyTotals totals = GetTotals(faction, player);
		totals.m_iStarting++;
		return totals;
	}

	//------------------------------------------------------------------------------------------------
	void RecordTotalDeath(string faction, bool player)
	{
		GC_CasualtyTotals totals = GetTotals(faction, player);
		totals.m_iTotalDeaths++;
	}

	//------------------------------------------------------------------------------------------------
	bool PassesDeathCount(array<string> factions, int selection, int comparison, int required)
	{
		int deaths;
		foreach (GC_CasualtyTotals totals : m_aTotals)
		{
			if (totals.Matches(factions, selection))
				deaths += totals.m_iTotalDeaths;
		}
		if (comparison == 0)
			return deaths > required;
		if (comparison == 1)
			return deaths < required;
		if (comparison == 2)
			return deaths == required;
		return deaths != required;
	}

'''+s[pos:];s=s.replace('''			if (factions && !factions.IsEmpty() && !factions.Contains(totals.m_sFaction))
				continue;
			if ((selection == 0 && !totals.m_bPlayers) || (selection == 1 && totals.m_bPlayers))''','\t\t\tif (!totals.Matches(factions, selection))');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_MissionCasualtyTracker.c';s=p.read_text().replace('protected ref GC_CasualtySnapshot m_Snapshot', 'protected ref array<IEntity> m_aRecordedDeaths = {};\n\tprotected ref GC_CasualtySnapshot m_Snapshot',1);s=s.replace('m_mLiving.Clear();','m_mLiving.Clear();\n\t\tm_aRecordedDeaths.Clear();',1);start=s.index('\tbool RecordDeath(');s=s[:start]+'''	bool RecordDeath(IEntity entity, GC_LobbyManager manager, int playerId)
	{
		SCR_ChimeraCharacter character = SCR_ChimeraCharacter.Cast(entity);
		if (!character || character.FindComponent(GC_LobbySpectatorBodyComponent) || m_aRecordedDeaths.Contains(entity))
			return false;
		GC_CasualtyTotals starting;
		m_mLiving.Find(entity, starting);
		GC_Slot slot = manager.GetSlotByEntity(entity);
		bool player = playerId > 0 || (starting && starting.m_bPlayers) || (slot && slot.HasClaim());
		if (!player)
		{
			if (slot)
				return false;
			AIControlComponent control = AIControlComponent.Cast(entity.FindComponent(AIControlComponent));
			if (control && control.GetAIAgent())
			{
				SCR_AIGroup group = SCR_AIGroup.Cast(control.GetAIAgent().GetParentGroup());
				if (group && group.IsPlayable())
					return false;
			}
		}
		m_aRecordedDeaths.Insert(entity);
		if (starting)
		{
			m_mLiving.Remove(entity);
			starting.m_iDeaths++;
		}
		m_Snapshot.RecordTotalDeath(character.GetFactionKey(), player);
		return true;
	}

	//------------------------------------------------------------------------------------------------
	//! Deletion releases weak entity keys without changing death totals or the starting denominator.
	void ForgetEntity(IEntity entity)
	{
		if (!entity)
			return;
		m_mLiving.Remove(entity);
		m_aRecordedDeaths.RemoveItem(entity);
	}
}
''';p.write_text(s)
p=r/'scripts/Game/GC_LobbyManager.c';s=p.read_text().replace('m_MissionCasualties.RecordDeath(victim)', 'm_MissionCasualties.RecordDeath(victim, this, instigatorContextData.GetVictimPlayerID())');start=s.index('override void OnControllableDeleted');pos=s.index('\n\t\tGC_Slot slot',start);s=s[:pos]+'\n\t\tif (m_MissionCasualties)\n\t\t\tm_MissionCasualties.ForgetEntity(entity);\n'+s[pos:];p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_RuleCondition.c';s=p.read_text().replace('float m_fCasualtyRatio;', 'float m_fCasualtyRatio;\n\tint m_iDeathComparison;\n\tint m_iRequiredDeaths;');s=s.replace('m_iCheck > 7','m_iCheck > 8');s=s.replace('if (m_iCheck == 7)\n\t\t{','if (m_iCheck == 7 || m_iCheck == 8)\n\t\t{',1);s=s.replace('if (m_iCasualtyCount < 0 || m_iCasualtyCount > 2 || !(m_fCasualtyRatio >= 0 && m_fCasualtyRatio <= 1))','''if (m_iCasualtyCount < 0 || m_iCasualtyCount > 2)
				return false;
			if (m_iCheck == 7 && !(m_fCasualtyRatio >= 0 && m_fCasualtyRatio <= 1))
				return false;
			if (m_iCheck == 8 && (m_iDeathComparison < 0 || m_iDeathComparison > 3 || m_iRequiredDeaths < 0 || m_iRequiredDeaths > 1000000))''');s=s.replace('if (m_iCheck == 7)\n\t\t\treturn true;', 'if (m_iCheck == 7 || m_iCheck == 8)\n\t\t\treturn true;');s=s.replace('else if (m_iCheck == 7)\n', 'else if (m_iCheck == 8)\n\t\t\tanswer = casualties && casualties.PassesDeathCount(m_aCasualtyFactions, m_iCasualtyCount, m_iDeathComparison, m_iRequiredDeaths);\n\t\telse if (m_iCheck == 7)\n');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_ConditionBase.c';s=p.read_text().replace('Casualties, Condition Group','Casualties, Total Deaths, Condition Group');p.write_text(s)
