from pathlib import Path
import re
root=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
d=root/'scripts/Game/MissionFramework'
def read(p): return p.read_text(encoding='utf-8-sig')
def write(p,s): p.write_text(s,encoding='utf-8',newline='\n')
# Separate compiled data from the single authorable mission rule.
old=read(d/'GC_MissionRule.c').replace('GC_MissionRule','GC_CompiledMissionRule').replace(' : GC_RuleDefinition','')
old=re.sub(r'^\[BaseContainerProps.*\n','',old,flags=re.M)
old=re.sub(r'^\t\[Attribute.*\n','',old,flags=re.M)
old=old.replace('override GC_CompiledMissionRule BuildRule()', 'GC_CompiledMissionRule BuildRule()')
write(d/'GC_CompiledMissionRule.c',old)
simple=read(d/'GC_SimpleMissionRule.c').replace('GC_MissionRule','GC_CompiledMissionRule').replace('GC_SimpleMissionRule : GC_RuleDefinition','GC_MissionRule').replace('override GC_CompiledMissionRule BuildRule()', 'GC_CompiledMissionRule BuildRule()')
write(d/'GC_MissionRule.c',simple)
(d/'GC_SimpleMissionRule.c').unlink()
(d/'GC_RuleDefinition.c').unlink()
for name in ['GC_MissionRuleEngine.c','GC_MissionEvent.c']:
 p=d/name; write(p,re.sub(r'\bGC_MissionRule\b','GC_CompiledMissionRule',read(p)))
for name in ['GC_MissionCondition.c','GC_MissionAction.c','GC_MissionEvent.c']:
 p=d/name; s=read(p).replace('[BaseContainerProps(visible: false, insertable: false)]\n','')
 if name=='GC_MissionCondition.c':
  a=s.index('[BaseContainerProps(category: "Advanced"), BaseContainerCustomStringTitleField("Test condition")]')
  b=s.index('[BaseContainerProps(category: "Advanced"), BaseContainerCustomStringTitleField("All conditions")]',a)
  s=s[:a]+s[b:]
  s=s.replace(': GC_CheckGroup', ': GC_MissionCondition')
  s=re.sub(r'(class GC_Check\w+ : GC_MissionCondition\n\{)',r'\1\n\t[Attribute("", UIWidgets.Object, "Conditions in this combination.")]\n\tref array<ref GC_MissionCondition> m_aRequirements;\n',s)
 write(p,s)
p=root/'scripts/Game/GC_LobbyManager.c';s=read(p)
s=re.sub(r'\bGC_MissionRule\b','GC_CompiledMissionRule',s).replace('GC_RuleDefinition','GC_MissionRule')
s=s.replace('m_MissionRuleEngine.Start(runtimeRules);','m_MissionRuleEngine.Start(runtimeRules, m_aMissionFlags);')
s=s.replace('\tprotected ref array<ref GC_MissionRule> m_aMissionRules;', '\tprotected ref array<ref GC_MissionRule> m_aMissionRules;\n\n\t[Attribute("", UIWidgets.Object, "Name the mission flags and choose their starting values. Rules can change these flags during the match.", category: "Lobby2")]\n\tprotected ref array<ref GC_MissionFlag> m_aMissionFlags;')
write(p,s)
# Migrate this project's simple rules; preserve the user's unrelated scene edits.
for p in [root/'Prefabs/MP/Modes/GameMode_Lobby_RulesExample.et',root/'tests/McpTestAddon/worlds/MissionRulesExample_Layers/default.layer']:
 s=read(p).replace('GC_SimpleMissionRule','GC_MissionRule').replace('GC_RuleDefinition','GC_MissionRule');write(p,s)
# Keep the historical serialized fixture as isolated test data, not manager configuration.
p=root/'tests/McpTestAddon/Prefabs/MissionRulesLegacy.et';s=read(p).replace('GC_MissionRule','GC_LegacyRuleFixture').replace('m_aMissionRules','m_aLegacyRuleFixtures');write(p,s)
p=root/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=read(p)
s=re.sub(r'\bGC_MissionRule\b','GC_CompiledMissionRule',s).replace('GC_SimpleMissionRule','GC_MissionRule').replace('GC_RuleDefinition','GC_MissionRule')
s=s.replace('GC_TestCondition yes = new GC_TestCondition();\n\t\tyes.m_iAnswer = 1;', 'GC_FlagCondition yes = new GC_FlagCondition();\n\t\tyes.m_sFlagName = "unset";\n\t\tyes.m_iMustBe = 0;')
s=s.replace('GC_TestCondition no = new GC_TestCondition();\n\t\tno.m_iAnswer = 0;', 'GC_FlagCondition no = new GC_FlagCondition();\n\t\tno.m_sFlagName = "unset";\n\t\tno.m_iMustBe = 1;')
a=s.index('\tstring RunLegacyExample()');b=s.index('\n}\n\nmodded class GC_LobbyManager',a)
part=s[a:b].replace('GetObjectArray("m_aMissionRules")','GetObjectArray("m_aLegacyRuleFixtures")').replace('GC_CompiledMissionRule.Cast(BaseContainerTools.CreateInstanceFromContainer(authoredRules.Get(index)))','GC_LegacyRuleFixture.Cast(BaseContainerTools.CreateInstanceFromContainer(authoredRules.Get(index))).BuildRule()')
s=s[:a]+part+s[b:]
a=s.index('\tstring GC_TestMissionRulesPrepare()');b=s.index('\n\t//---',a)
s=s[:a]+'''\tstring GC_TestMissionRulesPrepare()
\t{
\t\treturn "UNAVAILABLE: use the authored MissionRulesExample fixture";
\t}
'''+s[b:]
write(p,s)
# Historical data loader belongs only to the optional test addon.
fixture=read(d/'GC_CompiledMissionRule.c')
original=__import__('subprocess').check_output(['git','-C',str(root),'show','HEAD:scripts/Game/MissionFramework/GC_MissionRule.c'],text=True)
original=original[original.index('[BaseContainerProps(insertable: false)'):]
original=original.replace('GC_MissionRule : GC_RuleDefinition','GC_LegacyRuleFixture').replace('override GC_MissionRule BuildRule()', 'GC_CompiledMissionRule BuildRule()')
fields=['m_sName','m_bEnabled','m_eWhen','m_sTestEventName','m_fEverySeconds','m_If','m_aThen','m_bRunOnlyOnce','m_fWaitSeconds']
original=original.replace('return this;', 'GC_CompiledMissionRule rule = new GC_CompiledMissionRule();\n'+''.join('\t\trule.'+f+' = '+f+';\n' for f in fields)+'\t\treturn rule;')
write(root/'tests/McpTestAddon/scripts/Game/GC_LegacyRuleFixture.c',original+'\nmodded class GC_LobbyManager\n{\n\t[Attribute("", UIWidgets.Object)]\n\tref array<ref GC_LegacyRuleFixture> m_aLegacyRuleFixtures;\n}\n')
# Legacy actions still deserialize in the isolated fixture; these are unrelated to authorable action choices.
p=d/'GC_CompiledMissionRule.c';s=read(p); origaction=__import__('subprocess').check_output(['git','-C',str(root),'show','HEAD:scripts/Game/MissionFramework/GC_MissionRule.c'],text=True)
# Retain action/condition serialization for old fixture verification without putting them in new rule pickers.
a=origaction.index('[BaseContainerProps(),');b=origaction.index('[BaseContainerProps(insertable: false)')
x=s.index('class GC_RuleAction');y=s.index('class GC_CompiledMissionRule');s=s[:x]+origaction[a:b]+s[y:];write(p,s)
