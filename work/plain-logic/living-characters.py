from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework')
p=r/'GC_AreaTargets.c'
s=p.read_text()
s=s.replace('[BaseContainerProps(), BaseContainerCustomStringTitleField("Players / AI")]', '[BaseContainerProps(visible: false, insertable: false), BaseContainerCustomStringTitleField("Players / AI (legacy - use Living Characters)")]')
start=s.index('\t\tcheck.m_iAreaTargets = 1;')
end=s.index('\n\t}\n}',start)
s=s[:start]+'\t\treturn GC_LivingCharactersCondition.BuildPopulation(check, m_iWho, m_aFactions);'+s[end:]
s=s.replace('BaseContainerCustomStringTitleField("Target Count")','BaseContainerCustomStringTitleField("Count")').replace('class GC_AreaCount\n','class GC_AreaCount : GC_LivingRequirementBase\n')
s=s.replace('\n\tint m_iRequiredCount;\n}', '''
	int m_iRequiredCount;

	//------------------------------------------------------------------------------------------------
	override bool BuildRequirement(GC_RuleCondition check)
	{
		check.m_iAreaComparison = m_iComparison;
		check.m_iAreaRequired = m_iRequiredCount;
		return true;
	}
}''')
p.write_text(s)
p=r/'GC_AreaCondition.c';s=p.read_text().replace('Choose Named Entities for specific world objects, or Players / AI for living characters. Only the selected target type has settings. Spectators never count as Players / AI.', 'Choose Named Entities for specific world objects. To check living players or AI, add the Living Characters condition directly to the mission event instead.')
p.write_text(s)
p=r/'GC_RuleCondition.c';s=p.read_text().replace('\tbool m_bAreaOutside;', '\tbool m_bAreaOutside;\n\tbool m_bAreaAnywhere;\n\tbool m_bAreaProportion;\n\tfloat m_fAreaProportion;')
s=s.replace('if (m_sAreaName.IsEmpty() || m_iAreaTargets < 0', 'if ((!m_bAreaAnywhere && m_sAreaName.IsEmpty()) || m_iAreaTargets < 0')
s=s.replace('\t\tarray<string> keys = m_aAreaNames;', '''		if (m_bAreaAnywhere && (m_iAreaTargets != 1 || !m_sAreaName.IsEmpty() || m_bAreaOutside))
			return false;
		if (m_bAreaProportion && (m_iAreaTargets != 1 || m_bAreaAnywhere || !(m_fAreaProportion >= 0 && m_fAreaProportion <= 1)))
			return false;
		array<string> keys = m_aAreaNames;''')
s=s.replace('areas.GetObservation(this).Passes(m_bAreaOutside, m_iAreaComparison, m_iAreaRequired)', 'areas.GetObservation(this).PassesCondition(this)')
p.write_text(s)
p=r/'GC_MissionAreaTracker.c';s=p.read_text()
s=s.replace('if (m_Definition.m_sAreaName != other.m_sAreaName ||', 'if (m_Definition.m_bAreaAnywhere != other.m_bAreaAnywhere || m_Definition.m_sAreaName != other.m_sAreaName ||')
s=s.replace('bool available = m_Area.IsAvailable();', 'bool available = m_Definition.m_bAreaAnywhere || (m_Area && m_Area.IsAvailable());')
s=s.replace('if (m_Area.ContainsEntity(character.m_Entity, character.m_vPosition))', 'if (m_Definition.m_bAreaAnywhere || m_Area.ContainsEntity(character.m_Entity, character.m_vPosition))')
s=s.replace('if (!m_bAvailable || !m_Area.IsAvailable())', 'if (!m_bAvailable || (!m_Definition.m_bAreaAnywhere && (!m_Area || !m_Area.IsAvailable())))')
needle='\tbool Passes(bool outside, int comparison, int required)'
s=s.replace(needle, '''	bool PassesCondition(GC_RuleCondition check)
	{
		if (!IsAvailable())
			return false;
		if (check.m_bAreaProportion)
		{
			int count = m_iInside;
			if (check.m_bAreaOutside)
				count = m_iTotal - m_iInside;
			return PassesProportion(count, m_iTotal, check.m_fAreaProportion);
		}
		return Passes(check.m_bAreaOutside, check.m_iAreaComparison, check.m_iAreaRequired);
	}

	//------------------------------------------------------------------------------------------------
	//! Divide as floats so a matching threshold uses the same representation as the ratio.
	static bool PassesProportion(int count, int total, float required)
	{
		if (total <= 0 || count < 0 || count > total || !(required >= 0 && required <= 1))
			return false;
		float fraction = count;
		fraction /= total;
		return fraction >= required;
	}

	//------------------------------------------------------------------------------------------------
'''+needle)
s=s.replace('if (!m_mAreas.Find(check.m_sAreaName, area))', 'if (!check.m_bAreaAnywhere && !m_mAreas.Find(check.m_sAreaName, area))')
s=s.replace('observation.Passes(check.m_bAreaOutside, check.m_iAreaComparison, check.m_iAreaRequired)', 'observation.PassesCondition(check)')
p.write_text(s)
