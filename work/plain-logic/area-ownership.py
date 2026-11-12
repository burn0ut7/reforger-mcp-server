from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/scripts/Game/MissionFramework')
p=r/'GC_LivingCharactersCondition.c';s=p.read_text()
s=s.replace('[Attribute("", UIWidgets.Object, "Optional area filter. Leave unset to count living characters anywhere. Add Area to count Inside or Outside a named radius or polygon. This contains location settings only, not more conditions.")]', '[Attribute("", UIWidgets.Hidden, "Legacy area configuration. Move this check to Area Condition; legacy area-based Living Characters checks are rejected rather than silently counting everywhere.")]')
s=s.replace('[Attribute("", UIWidgets.Object, "Choose Count or Proportion. Leave unset to require at least one matching living character. Proportion requires an Area and compares that side with all matching living characters everywhere. There are no child conditions here.")]\n\tref GC_LivingRequirementBase m_Requirement;', '[Attribute("", UIWidgets.Object, "Optional living-character count anywhere in the mission. Leave unset to require at least one. For Inside/Outside or extraction proportions, use Area Condition with Players / AI targets.")]\n\tref GC_AreaCount m_Requirement;')
s=s.replace('\t\tGC_RuleCondition check = new GC_RuleCondition();','''		if (m_Area)
		{
			Print("GC | Mission Framework | Move legacy Living Characters Area settings to Area Condition. This condition is disabled until migrated.", LogLevel.ERROR);
			return null;
		}
		GC_RuleCondition check = new GC_RuleCondition();''',1)
s=s.replace('check.m_bAreaAnywhere = !m_Area;', 'check.m_bAreaAnywhere = true;').replace('!BuildPopulation(check, m_iWho, m_aFactions)', '!GC_AreaPopulation.BuildSelection(check, m_iWho, m_aFactions)')
a=s.index('\n\t\tif (m_Area)\n',s.index('check.m_bAreaAnywhere'))
b=s.index('\n\t\tif (m_Requirement',a)
s=s[:a]+s[b:]
a=s.index('\n\t//------------------------------------------------------------------------------------------------\n\t//! Shared with')
b=s.index('\n}\n',a)
helper=s[a:b]
s=s[:a]+s[b:]
s=s.replace('[BaseContainerProps(), BaseContainerCustomStringTitleField("Area")]', '[BaseContainerProps(visible: false, insertable: false), BaseContainerCustomStringTitleField("Legacy area - migrate to Area Condition")]')
p.write_text(s)
p=r/'GC_AreaPopulation.c';t=p.read_text();idx=t.rindex('\n}')
helper=helper.replace('//! Shared with the hidden serialized Players / AI target from the previous prototype.','//! Shared target filters for general living counts and area conditions.').replace('static bool BuildPopulation(', 'static bool BuildSelection(')
t=t[:idx]+helper+t[idx:];p.write_text(t)
p=r/'GC_AreaTargets.c';s=p.read_text().replace('[BaseContainerProps(visible: false, insertable: false), BaseContainerCustomStringTitleField("Players / AI (legacy - use Living Characters)")]', '[BaseContainerProps(), BaseContainerCustomStringTitleField("Players / AI")]').replace('GC_LivingCharactersCondition.BuildPopulation(', 'GC_AreaPopulation.BuildSelection(')
s=s.replace('Number of targets to compare against. Inside counts targets inside; Outside counts targets outside. Outside does not mean all targets are outside.', 'Number of targets to compare against. Living Characters counts anywhere. Area Condition counts targets on its selected side; Outside does not mean all targets are outside.')
p.write_text(s)
p=r/'GC_AreaCondition.c';s=p.read_text().replace('Choose Named Entities for specific world objects. To check living players or AI, add the Living Characters condition directly to the mission event instead.', 'Choose Named Entities for specific world objects, or Players / AI for living characters with optional faction filters. These are target settings, not nested conditions.').replace('Count can change that requirement.', 'Requirement can change that count or require a proportion of living characters.')
s=s.replace('[Attribute("", UIWidgets.Object, "Optional number of matching targets. Leave unset to require at least one target on the selected side. Count compares targets, not child conditions.")]\n\tref GC_AreaCount m_Count;', '''[Attribute("", UIWidgets.Object, "Choose Count, or Proportion for Players / AI. Leave unset to require at least one matching target on the selected side. Proportion 0.9 means at least 90% of all eligible living characters are on that side. No eligible living characters means false. Named Entities supports Count only.")]
	ref GC_LivingRequirementBase m_Requirement;

	[Attribute("", UIWidgets.Hidden, "Legacy Count. Used only when Requirement is unset, preserving older saved area checks.")]
	ref GC_AreaCount m_Count;''')
a=s.index('\t\tif (m_Count)');b=s.index('\n\t\tif (m_iCheck',a)
s=s[:a]+'''		GC_LivingRequirementBase requirement = m_Requirement;
		if (!requirement)
			requirement = m_Count;
		if (requirement && !requirement.BuildRequirement(condition))
			return null;'''+s[b:]
p.write_text(s)
