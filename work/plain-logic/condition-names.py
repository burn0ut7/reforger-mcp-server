from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
labels={'Condition Group':'Group Condition','Condition Count':'Count Condition','Game Mode State':'Game Mode State Condition','Random Chance':'Random Chance Condition','Casualties':'Casualties Condition','Total Deaths':'Total Deaths Condition','Entity Damage':'Entity Damage Condition','Living Characters':'Living Characters Condition'}
for p in (r/'scripts/Game/MissionFramework').glob('*.c'):
 s=p.read_text(encoding='utf-8-sig'); t=s
 for old,new in labels.items():
  t=t.replace('BaseContainerCustomStringTitleField("'+old+'")', 'BaseContainerCustomStringTitleField("'+new+'")')
 t=t.replace('Condition Group','Group Condition').replace('Condition Count','Count Condition')
 if p.name=='GC_ConditionBase.c':
  t=t.replace('Choose Mission Flag Condition, Mission Event Condition, Game Mode State, Random Chance, Casualties, Total Deaths, Entity Damage, Area Condition, Group Condition or Count Condition instead.', 'Choose Mission Flag Condition, Mission Event Condition, Game Mode State Condition, Random Chance Condition, Casualties Condition, Total Deaths Condition, Entity Damage Condition, Living Characters Condition, Area Condition, Group Condition or Count Condition instead.')
 if t!=s:p.write_text(t,encoding='utf-8')
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8').replace('Condition Group','Group Condition').replace('Condition Count','Count Condition')
s+='\n\n### Condition naming\n\nEvery selectable condition uses the visible name **[Name] Condition**: Area Condition, Casualties Condition, Count Condition, Entity Damage Condition, Game Mode State Condition, Group Condition, Living Characters Condition, Mission Event Condition, Mission Flag Condition, Random Chance Condition, and Total Deaths Condition. The nonselectable base remains **Do not use**. Serialized class names and field names are unchanged, so saved missions require no migration for this naming update. Targets and Requirement settings are not conditions and retain their short names.\n'
p.write_text(s,encoding='utf-8')
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text(encoding='utf-8').replace('Condition Group','Group Condition').replace('Condition Count','Count Condition');p.write_text(s,encoding='utf-8')
