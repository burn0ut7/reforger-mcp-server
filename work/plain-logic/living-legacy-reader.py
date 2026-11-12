from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'tests/McpTestAddon/scripts/Game/GC_LivingCharactersTests.c';s=p.read_text().replace('condition.m_Requirement = count;', 'condition.m_Count = count;')
a=s.index('\t\tcondition.m_Area = new GC_LivingCharacterArea();')
s=s[:a]+'''		condition.m_Requirement = proportion;
		Check(!condition.BuildCheck(), "legacy proportion without area cannot silently become an anywhere count");
		condition.m_Requirement = count;
		condition.m_Count = null;
		Check(condition.BuildCheck().m_iAreaRequired == 5, "legacy living count remains readable");
'''+s[a:]
p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8').replace('optional **Requirement > Count**. It counts', 'optional **Count**. It counts').replace('Leave Requirement unset for Greater than 0. Count supports', 'New checks with Count unset use Greater than 0. Count supports').replace('Living Characters now accepts Count only. A hidden legacy Area reader rejects old area-based checks with a migration diagnostic, so they cannot silently broaden to count everyone everywhere.', 'Living Characters now exposes Count only. Its original Requirement remains a hidden compatible reader: old Counts still apply until an explicit Count is chosen, and old Proportions are rejected, including when their Area was unset. The hidden legacy Area reader also rejects old area-based checks with a migration diagnostic. Neither can silently broaden to count everyone everywhere.')
p.write_text(s,encoding='utf-8')
