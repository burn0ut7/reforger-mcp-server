from pathlib import Path
import re
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
def edit(f,fn):
 p=r/f;p.write_text(fn(p.read_text(encoding='utf-8')),encoding='utf-8')
def replace_event(s,state,id):
 return re.sub(r'     m_Event GC_MissionEvent "\{'+id+r'\}" \{\n.*?\n     \}', '     m_aConditions {\n      GC_GameModeStateCondition "{'+id+'}" {\n       m_eGameModeState '+str(state)+'\n      }\n     }',s,flags=re.S)
f='Prefabs/MP/Modes/GameMode_Lobby_RulesExample.et'
edit(f,lambda s:replace_event(replace_event(s,1,'7B115D5DFF7EDA10'),2,'7B115D5DFF7EDA11').replace('1. Start the test','1. Match begins').replace('2. Finish the test','2. After action begins').replace('The flag started this rule.','After action started this event.'))
f='Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et'
edit(f,lambda s:replace_event(s,1,'7B115D5DFF7EDA21').replace('     m_aActions {','     m_Advanced GC_RuleOptions "{7B115D5DFF7EDA25}" {\n      m_fWaitSeconds 2\n     }\n     m_aActions {'))
f='worlds/examples/arland/Arland_Lobby_Layers/default.layer';edit(f,lambda s:replace_event(s,1,'6AD24908E66798EE'))
f='tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';p=r/f;s=p.read_text(encoding='utf-8');a=s.index('\t\tGC_MissionEvent trigger');b=s.index('\t\tGC_TestFixedCondition yes',a)
s=s[:a]+'''		simple.m_Advanced = new GC_RuleOptions();
		simple.m_Advanced.m_bDisabled = true;
		engine.Start({simple.BuildRule()});
		Check(engine.GetRunCount() == 0, "disabled simple rule stays off");
		simple.m_Advanced.m_bDisabled = false;
		simple.m_Advanced.m_iRun = 2;
		Check(!simple.BuildRule(), "bad repeat setting rejected");
		simple.m_Advanced.m_iRun = 0;
		simple.m_Advanced.m_fWaitSeconds = -1;
		Check(!simple.BuildRule(), "bad delay rejected");
		simple.m_Advanced.m_fWaitSeconds = 2;
		engine.Start({simple.BuildRule()});
		engine.Advance(1);
		Check(engine.GetRunCount() == 0, "simple wait delays actions");
		engine.Advance(2);
		Check(engine.GetRunCount() == 1, "simple wait completes");
		engine.Advance(4);
		Check(engine.GetRunCount() == 1, "simple wait defaults once");
		simple.m_Advanced = null;

'''+s[b:];s=s.replace('\t\tsimple.m_Event = null;\n','')
s=s.replace('engine.Start(rules);\n\t\tCheck(engine.GetInvalidRuleCount() == 0, "simple example deserializes");\n\t\tCheck(engine.GetFlag("test_ready") && engine.GetFlag("test_complete"), "flag change starts next rule");','engine.Start(rules, null, SCR_EGameModeState.GAME);\n\t\tCheck(engine.GetInvalidRuleCount() == 0 && engine.GetFlag("test_ready") && !engine.GetFlag("test_complete"), "match state starts only first example");\n\t\tengine.SetGameModeState(SCR_EGameModeState.POSTGAME);\n\t\tCheck(engine.GetFlag("test_ready") && engine.GetFlag("test_complete"), "after action state starts second example");')
s=s.replace('Check(!rules[0].m_If, "serialized example needs no placeholder conditions");','Check(rules[0].m_If && rules[0].m_If.UsesGameModeState(), "serialized example uses Game Mode State");').replace('engine.Start(rules);\n\t\tCheck(engine.GetInvalidRuleCount() == 0, "no-flags example deserializes");','engine.Start(rules, null, SCR_EGameModeState.GAME);\n\t\tCheck(engine.GetInvalidRuleCount() == 0, "no-flags example deserializes");').replace('"no-flags timer waits"','"no-flags match-start wait"').replace('"random delay starts after timer"','"combined fixed and random delay has not elapsed"');p.write_text(s,encoding='utf-8')
