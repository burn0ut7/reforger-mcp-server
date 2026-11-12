from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
for folder in ['scripts','tests/McpTestAddon','Prefabs','worlds']:
 for p in (r/folder).rglob('*'):
  if p.suffix.lower() not in ['.c','.et','.layer','.ent','.conf']: continue
  b=p.read_bytes();updated=b.replace(b'GC_ConditionBase_DoNotUse',b'GC_ConditionBase')
  if updated!=b:p.write_bytes(updated)
p=r/'scripts/Game/MissionFramework/GC_ConditionBase_DoNotUse.c';s=p.read_text().replace('BaseContainerCustomStringTitleField("Do not use - condition base")','BaseContainerCustomStringTitleField("Do not use")');p.write_text(s);p.rename(p.with_name('GC_ConditionBase.c'))
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('GC_ConditionBase_DoNotUse','GC_ConditionBase').replace('**Do not use - condition base**','**Do not use**').replace('The class name itself includes Base and DoNotUse so the warning remains clear in class pickers.', 'The class name identifies it as a base; the display-title attribute says Do not use.');p.write_text(s)
