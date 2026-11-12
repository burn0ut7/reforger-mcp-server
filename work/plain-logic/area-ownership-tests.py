from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'tests/McpTestAddon/scripts/Game/GC_LivingCharactersTests.c';s=p.read_text()
a=s.index('\t\tGC_LivingProportion proportion');b=s.index('\t\tCheck(!extraction.Evaluate',a)
s=s[:a]+'''		GC_AreaCondition area = new GC_AreaCondition();
		GC_CharacterAreaTargets targets = new GC_CharacterAreaTargets();
		targets.m_aFactions = {"US"};
		area.m_Targets = targets;
		GC_LivingProportion proportion = new GC_LivingProportion();
		proportion.m_fRequiredProportion = 0.9;
		area.m_Requirement = proportion;
		Check(!area.BuildCheck(), "blank area is invalid");
		area.m_sArea = "Extraction";
		GC_RuleCondition extraction = area.BuildCheck();
		Check(extraction && extraction.m_bAreaProportion && !extraction.m_bAreaAnywhere, "area extraction builds one condition leaf");
'''+s[b:]
s=s.replace('condition.m_Requirement = count;\n\t\tGC_RuleCondition areaCount = condition.BuildCheck();', 'area.m_Requirement = count;\n\t\tGC_RuleCondition areaCount = area.BuildCheck();')
s=s.replace('condition.m_Requirement = proportion;', 'area.m_Requirement = proportion;').replace('Check(!condition.BuildCheck(), "ratio above one rejected");', 'Check(!area.BuildCheck(), "ratio above one rejected");').replace('Check(!condition.BuildCheck(), "negative ratio rejected");', 'Check(!area.BuildCheck(), "negative ratio rejected");')
idx=s.index('\t\treturn string.Format')
s=s[:idx]+'''		GC_NamedAreaTargets named = new GC_NamedAreaTargets();
		named.m_aNames = {"Truck"};
		area.m_Targets = named;
		Check(!area.BuildCheck(), "named entities do not accept living proportions");
		area.m_Requirement = null;
		area.m_Count = count;
		Check(area.BuildCheck().m_iAreaRequired == 5, "legacy area count remains readable");
		GC_AreaCount replacement = new GC_AreaCount();
		replacement.m_iRequiredCount = 2;
		area.m_Requirement = replacement;
		Check(area.BuildCheck().m_iAreaRequired == 2, "visible requirement overrides legacy count");
		condition.m_aFactions = {};
		condition.m_Area = new GC_LivingCharacterArea();
		Check(!condition.BuildCheck(), "legacy living area cannot silently become an anywhere count");
'''+s[idx:]
p.write_text(s)
