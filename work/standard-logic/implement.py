from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
d=r/'scripts/Game/MissionFramework'
p=d/'GC_MissionCondition.c'
s=p.read_text(encoding='utf-8');s=s[:s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("All conditions')]
groups=[('GC_AllConditions','All conditions (AND)','Every condition must be true.','ALL'),('GC_AnyCondition','Any condition (OR)','At least one condition must be true.','ANY'),('GC_NoneConditions','None of the conditions (NOR)','No condition may be true. With one child, this means NOT that condition.','NONE'),('GC_NotAllConditions','Not all conditions (NAND)','At least one condition must be false.','NOT_ALL'),('GC_XorConditions','Odd number true (XOR)','With two conditions, exactly one must be true. With more conditions, 1, 3, 5, or another odd number must be true.','ODD_TRUE'),('GC_XnorConditions','Even number true (XNOR)','With two conditions, both must give the same answer. With more conditions, 0, 2, 4, or another even number must be true.','EVEN_TRUE')]
for cls,title,desc,kind in groups:
 s+=f'''[BaseContainerProps(), BaseContainerCustomStringTitleField("{title}")]
class {cls} : GC_MissionCondition
{{
\t[Attribute("", UIWidgets.Object, "{desc} Add at least one condition. Conditions can contain other groups.")]
\tref array<ref GC_MissionCondition> m_aConditions;

\t//------------------------------------------------------------------------------------------------
\toverride GC_RuleCondition BuildCheck(int depth = 0)
\t{{
\t\tif (!m_aConditions || m_aConditions.IsEmpty())
\t\t\treturn null;
\t\treturn BuildGroup(m_aConditions, GC_ERuleCheck.{kind}, 0, depth);
\t}}
}}

'''
s+='''[BaseContainerProps(), BaseContainerCustomStringTitleField("True or false value")]
class GC_BooleanCondition : GC_MissionCondition
{
\t[Attribute("0", UIWidgets.CheckBox, "Checked means true; unchecked means false. This is a fixed logic value, not a mission flag or a live game check.")]
\tbool m_bValue;

\t//------------------------------------------------------------------------------------------------
\toverride GC_RuleCondition BuildCheck(int depth = 0)
\t{
\t\tif (depth > 16)
\t\t\treturn null;
\t\tGC_RuleCondition check = new GC_RuleCondition();
\t\tcheck.m_iCheck = 2;
\t\tcheck.m_bTestAnswer = m_bValue;
\t\treturn check;
\t}
}
'''
p.write_text(s,encoding='utf-8')
# Each comparator owns only its two numeric inputs. No additional base choice or type dropdown.
ops=[('Equal','Equal to (=)','First number must equal Second number.','=='),('NotEqual','Not equal to (!=)','First number must differ from Second number.','!='),('LessThan','Less than (<)','First number must be smaller than Second number.','<'),('LessOrEqual','Less than or equal (<=)','First number must be smaller than or equal to Second number.','<='),('GreaterThan','Greater than (>)','First number must be larger than Second number.','>'),('GreaterOrEqual','Greater than or equal (>=)','First number must be larger than or equal to Second number.','>=')]
s='''//! Fixed numeric inputs for the logic foundation. Live game-value sources are a future slice.
class GC_NumberConditionValue
{
\t//------------------------------------------------------------------------------------------------
\t//! Positive comparisons reject non-finite values as well as numbers outside the editor range.
\tstatic bool IsValid(float number)
\t{
\t\treturn number >= -1000000000 && number <= 1000000000;
\t}
}

'''
for name,title,desc,op in ops:
 s+=f'''[BaseContainerProps(), BaseContainerCustomStringTitleField("{title}")]
class GC_{name}Condition : GC_MissionCondition
{{
\t[Attribute("0", UIWidgets.SpinBox, "{desc} Fixed number; decimals allowed. Range: -1 billion to 1 billion.", "-1000000000 1000000000 0.1")]
\tfloat m_fFirstNumber;
\t[Attribute("0", UIWidgets.SpinBox, "The fixed number to compare against. This does not read a game value.", "-1000000000 1000000000 0.1")]
\tfloat m_fSecondNumber;

\t//------------------------------------------------------------------------------------------------
\toverride GC_RuleCondition BuildCheck(int depth = 0)
\t{{
\t\tif (depth > 16 || !GC_NumberConditionValue.IsValid(m_fFirstNumber) || !GC_NumberConditionValue.IsValid(m_fSecondNumber))
\t\t\treturn null;
\t\tGC_RuleCondition check = new GC_RuleCondition();
\t\tcheck.m_iCheck = 2;
\t\tcheck.m_bTestAnswer = m_fFirstNumber {op} m_fSecondNumber;
\t\treturn check;
\t}}
}}

'''
(d/'GC_NumberConditions.c').write_text(s,encoding='utf-8')
# The first example's old flag check was redundant at match start. Keep its original outcomes.
p=r/'Prefabs/MP/Modes/GameMode_Lobby_RulesExample.et';s=p.read_text();s=s.replace('''     m_aConditions {
      GC_FlagOffCondition "{7B115D5DFF7EDA13}" {
       m_sFlagName "test_complete"
      }
     }
''','');p.write_text(s,encoding='utf-8')
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=p.read_text()
s=s.replace('GC_FlagOffCondition required = new GC_FlagOffCondition();\n\t\trequired.m_sFlagName = "ready";','GC_BooleanCondition required = new GC_BooleanCondition();\n\t\trequired.m_bValue = true;').replace('Off requirement accepts a missing flag','true value allows execution')
s=s.replace('GC_FlagOnCondition requiredOn = new GC_FlagOnCondition();\n\t\trequiredOn.m_sFlagName = "ready";','GC_BooleanCondition requiredOn = new GC_BooleanCondition();\n\t\trequiredOn.m_bValue = false;').replace('On requirement blocks without flag','false value blocks execution')
s=s.replace('GC_FlagOffCondition yes = new GC_FlagOffCondition();\n\t\tyes.m_sFlagName = "unset";','GC_BooleanCondition yes = new GC_BooleanCondition();\n\t\tyes.m_bValue = true;').replace('GC_FlagOnCondition no = new GC_FlagOnCondition();\n\t\tno.m_sFlagName = "unset";','GC_BooleanCondition no = new GC_BooleanCondition();\n\t\tno.m_bValue = false;')
s=s.replace('GC_NotCondition invert = new GC_NotCondition();','GC_NoneConditions invert = new GC_NoneConditions();').replace('invert.m_Condition = no;','invert.m_aConditions = {no};').replace('invert.m_Condition = any;','invert.m_aConditions = {any};').replace('invert.m_Condition = null;','invert.m_aConditions = {};').replace('invert.m_Condition = incomplete;','invert.m_aConditions = {incomplete};').replace('invert.m_Condition = invert;','invert.m_aConditions = {invert};').replace('Not ', 'None ').replace('All and Any objects','All and Any objects')
# Break a test self-reference before assigning a new array.
s=s.replace('Check(!invert.BuildCheck(), "None cycle is bounded");\n\t\tinvert.m_aConditions = {};','Check(!invert.BuildCheck(), "None cycle is bounded");\n\t\tinvert.m_aConditions.Clear();')
p.write_text(s,encoding='utf-8')
