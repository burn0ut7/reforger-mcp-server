from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
changes={
'GC_CasualtiesCondition.c':('m_fCasualtyRatio','0.01'),
'GC_EntityDamageCondition.c':('m_fDamageNeeded','0.01'),
'GC_Event.c':('m_fRandomChance','0.0001'),
'GC_RandomChanceCondition.c':('m_fChance','0.0001')}
for name,(field,step) in changes.items():
 p=r/'scripts/Game/MissionFramework'/name
 lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True)
 matches=[i for i,l in enumerate(lines) if l.strip()==f'float {field};']
 assert len(matches)==1
 i=matches[0]-1
 a=lines[i]
 assert '[Attribute(' in a
 a=a.replace('UIWidgets.EditBox','UIWidgets.Slider').replace('UIWidgets.SpinBox','UIWidgets.Slider')
 if '"0 1 0.01"' in a: a=a.replace('"0 1 0.01"', f'"0 1 {step}"')
 else: a=a.replace('")]','", "0 1 '+step+'")]')
 lines[i]=a
 p.write_text(''.join(lines),encoding='utf-8')
p=r/'docs/MissionRules/README.md'
s=p.read_text(encoding='utf-8')+'\n\n### Normalized value sliders\n\nAll visible Lobby2 editor float settings whose domain is 0..1 use UIWidgets.Slider: event Random Chance, scheduled Random Chance, Casualty Ratio, Damage Amount, and area Required Proportion. Chance sliders use step 0.0001 (0.01 percentage points), preserving selections such as 0.0005 = 0.05%; other ratio sliders use step 0.01. Defaults, serialized field names and runtime validation are unchanged. This does not reinterpret existing saved values or convert legacy 0..100 percentage fields. General configurable float parameters, time and distance values retain their own ranges.\n'
p.write_text(s,encoding='utf-8')
