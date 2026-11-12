from pathlib import Path
import re
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
# old name, new checkbox, tooltip, owner file; numeric meaning is unchanged.
items=[
('GC_MissionFlag','m_iStartingValue','m_bStartsTrue','Checked: the flag starts true. Unchecked: it starts false. Applied before match-start events, without sending a flag-change notification.','GC_MissionFlag.c'),
('GC_RuleRandom','m_iActionSelection','m_bPickOneRandomAction','Checked: choose one random action. Unchecked: run all actions in list order.','GC_MissionFlag.c'),
('GC_RuleOptions','m_iRun','m_bRepeatable','Checked: run again on later matching notifications. Unchecked: run once. This does not run continuously while conditions are true.','GC_Event.c'),
('GC_SetFlagAction','m_iSetTo','m_bSetTrue','Checked: set true, calling a declared flag with its conditions and count rules. Unchecked: set false without resetting the count.','GC_MissionAction.c'),
('GC_MissionFlagCondition','m_iMatch','m_bRequireFalse','Checked: require the flag to be false. Unchecked: require true. Reads the remembered result; never rechecks the flag conditions. Repeatable flags are true only during their check cycle.','GC_FlagConditions.c'),
('GC_MissionEventCondition','m_iMatch','m_bRequireFalse','Checked: require the event result to be false. Unchecked: require true. A non-repeatable event remembers true after running; a repeatable event resets after its check cycle. Missing or duplicate names never pass.','GC_MissionEventCondition.c'),
('GC_GameModeStateCondition','m_iMatch','m_bRequireOtherState','Checked: true when the game is in another state. Unchecked: true when in the selected state. Checked at startup and state changes, not every frame.','GC_GameModeStateCondition.c'),
('GC_RandomChanceCondition','m_iRollBehavior','m_bRollOnce','Checked: roll once after Check Interval and keep that result, true or false, for this Match. Unchecked: keep rolling every Check Interval. Event Repeatable controls its actions separately.','GC_RandomChanceCondition.c'),
('GC_ConditionCount','m_iCountFrom','m_bCountFlagCalls','Checked: count accepted calls to Mission Flag Name. Unchecked: count true entries in Conditions. Only the selected source is used.','GC_NumberConditions.c'),
('GC_AreaCondition','m_iCheck','m_bOutside','Checked: count targets outside the area. Unchecked: count targets inside. Requirement controls how many are needed. Checked once per second; brief crossings can be missed. Missing targets or invalid areas never pass.','GC_AreaCondition.c'),
('GC_LivingCharacterArea','m_iSide','m_bOutside','Checked: count characters outside. Unchecked: count characters inside. Uses each character origin.','GC_LivingCharactersCondition.c')]
unique={a:b for _,a,b,_,_ in items if a not in ['m_iMatch','m_iCheck','m_iSide']}
for f in list((p/'scripts').rglob('*.c'))+list((p/'tests').rglob('*.c')):
 s=f.read_text();before=s
 for a,b in unique.items(): s=re.sub(r'\b'+a+r'\b',b,s)
 if s!=before:f.write_text(s)
for cls,old,new,tip,fn in items:
 f=p/'scripts/Game/MissionFramework'/fn;s=f.read_text();start=s.index('class '+cls);end=s.find('\nclass ',start+1);end=end if end!=-1 else len(s);part=s[start:end]
 if old not in unique:part=re.sub(r'\b'+old+r'\b',new,part)
 part=re.sub(r'\[Attribute\("([01])", UIWidgets.ComboBox,[^\n]+\)\]\n\tint '+new+r';',lambda m:'[Attribute("'+m[1]+'", UIWidgets.CheckBox, "'+tip+'")]\n\tbool '+new+';',part)
 # Remove obsolete numeric-domain checks, preserving other validation.
 part=re.sub(r'\b'+new+r' < 0 \|\| '+new+r' > 1 \|\| ', '',part)
 part=re.sub(r' \|\| '+new+r' < 0 \|\| '+new+r' > 1', '',part)
 part=part.replace(new+' >= 0 && '+new+' <= 1 && ', '')
 part=part.replace(new+' == 1',new).replace(new+' == 0','!'+new).replace(new+' != 0',new)
 s=s[:start]+part+s[end:];f.write_text(s)
# Remaining external numeric validations for converted properties.
f=p/'scripts/Game/MissionFramework/GC_Event.c';s=f.read_text().replace('advanced.m_bRepeatable < 0 || advanced.m_bRepeatable > 1 || ', '').replace('advanced.m_bRepeatable == 0','!advanced.m_bRepeatable');f.write_text(s)
f=p/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=f.read_text().replace(' || flag.m_bStartsTrue < 0 || flag.m_bStartsTrue > 1','').replace('flag.m_bStartsTrue == 1','flag.m_bStartsTrue');f.write_text(s)
f=p/'scripts/Game/MissionFramework/GC_MissionAction.c';s=f.read_text().replace('\t\tif (m_bSetTrue < 0 || m_bSetTrue > 1)\n\t\t\treturn null;\n','');f.write_text(s)
# Typed test references with reused field names.
for fn,var,new in [('GC_GameModeConditionTests.c','condition','m_bRequireOtherState'),('GC_GameModeConditionTests.c','invalid','m_bRequireOtherState'),('GC_EventGroupTests.c','notPreview','m_bRequireOtherState'),('GC_MissionRuleTests.c','state','m_bRequireOtherState')]:
 f=p/'tests/McpTestAddon/scripts/Game'/fn;s=f.read_text().replace(var+'.m_iMatch',var+'.'+new);f.write_text(s)
# Booleans cannot have invalid enum values; retain meaningful validation checks.
for fn,oldline,checkline in [('GC_GameModeConditionTests.c','invalid.m_bRequireOtherState = 2;','Check(!invalid.BuildCheck(), "invalid comparison rejected");'),('GC_MissionRuleTests.c','simple.m_Advanced.m_bRepeatable = 2;','Check(!simple.BuildRule(), "bad repeat setting rejected");'),('GC_RandomConditionTests.c','random.m_bRollOnce = 2;','Check(!random.BuildCheck(), "unknown behavior rejected");')]:
 f=p/'tests/McpTestAddon/scripts/Game'/fn;s=f.read_text();s=re.sub(r'^\s*'+re.escape(oldline)+r'\n\s*'+re.escape(checkline)+r'\n','\n',s,flags=re.M);f.write_text(s)
# Change terminology only in user-facing attributes, not identifiers/engine enums.
for f in (p/'scripts/Game/MissionFramework').glob('*.c'):
 s=f.read_text();lines=[]
 for line in s.splitlines(True):
  if '[Attribute(' in line:
   line=re.sub(r'\bOn\b','true',line);line=re.sub(r'\bOff\b','false',line);line=line.replace('yes or no','true or false').replace('yes/no','true/false').replace('Flag is yes','Flag is true').replace('Advanced > Run is Every time','Advanced > Repeatable is checked')
  lines.append(line)
 f.write_text(''.join(lines))
# Migrate text resources by owning class, including nested containers. Preserve values.
byclass={cls:(old,new) for cls,old,new,_,_ in items}
changed=[]
for folder in ['Prefabs','worlds','tests']:
 for f in (p/folder).rglob('*'):
  if f.suffix not in ['.et','.ent','.layer']:continue
  s=f.read_text();stack=[];lines=[]
  for line in s.splitlines(True):
   match=re.match(r'\s*(GC_\w+)(?:\s+[^{}]*)?\s*\{\s*$',line)
   if match: stack.append(match[1])
   elif '{' in line: stack.extend([None]*line.count('{'))
   if stack and stack[-1] in byclass:
    a,b=byclass[stack[-1]];line=re.sub(r'^(\s*)'+a+r'\b',r'\1'+b,line)
   for _ in range(line.count('}')):
    if stack:stack.pop()
   lines.append(line)
  out=''.join(lines)
  if out!=s:f.write_text(out);changed.append(str(f.relative_to(p)))
print('Migrated resources:',changed)
