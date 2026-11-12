from pathlib import Path
import re
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
for folder in ['scripts','tests/McpTestAddon','Prefabs','worlds']:
 for p in (r/folder).rglob('*'):
  if p.suffix.lower() not in ['.c','.et','.layer','.ent','.conf']: continue
  b=p.read_bytes(); updated=re.sub(rb'\bGC_MissionCondition\b',b'GC_ConditionBase_DoNotUse',b)
  if updated!=b: p.write_bytes(updated)
p=r/'scripts/Game/MissionFramework/GC_MissionCondition.c';s=p.read_text().replace('BASE - Do not use directly (choose a specific condition)', 'Do not use - condition base');p.write_text(s);p.rename(p.with_name('GC_ConditionBase_DoNotUse.c'))
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('**GC_MissionCondition**, the required shared base','**GC_ConditionBase_DoNotUse**, the required shared base').replace('**BASE - Do not use directly (choose a specific condition)**','**Do not use - condition base**').replace('The serialized class name is retained so existing saved entries remain identifiable.', 'The class name itself includes Base and DoNotUse so the warning remains clear in class pickers.');s+='\nThe former GC_MissionCondition shared base is renamed GC_ConditionBase_DoNotUse, in GC_ConditionBase_DoNotUse.c. Remove the old GC_MissionCondition.c when installing a source snapshot. Update script references and any saved base entries to the new name; the current project was migrated. Base entries still do not perform a check and must be replaced with a specific condition before use.\n';p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace('GC_MissionEvent.c, GC_MissionRule.c,','GC_MissionEvent.c, GC_MissionRule.c, GC_MissionCondition.c,').replace("('GC_MissionEvent.c','GC_MissionRule.c',", "('GC_MissionEvent.c','GC_MissionRule.c','GC_MissionCondition.c',");p.write_text(s)
