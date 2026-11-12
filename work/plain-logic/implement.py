from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
d=r/'scripts/Game/MissionFramework'
p=d/'GC_MissionCondition.c';s=p.read_text();a=s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("Odd number true');b=s.index('[BaseContainerProps(), BaseContainerCustomStringTitleField("True or false value")]',a);s=s[:a]+s[b:]
for old,new in [('All conditions (AND)','All are true (AND)'),('Any condition (OR)','At least one is true (OR)'),('None of the conditions (NOR)','None are true (NOR)'),('Not all conditions (NAND)','At least one is false (NAND)')]:s=s.replace(old,new)
s=s.replace('No condition may be true. With one child, this means NOT that condition.', 'Every condition must be false. With one condition, this reverses its answer.').replace('At least one condition must be false. Add','This passes unless every condition is true. Add')
p.write_text(s,encoding='utf-8')
p=d/'GC_RuleCondition.c';s=p.read_text().replace('NOT_ALL,\n\tODD_TRUE,\n\tEVEN_TRUE','NOT_ALL,\n\t// Values 8 and 9 belonged to removed XOR/XNOR modes; do not reuse them.\n\tLESS_THAN = 10,\n\tGREATER_THAN')
s=s.replace('m_eGroupNeeds > GC_ERuleCheck.EVEN_TRUE','m_eGroupNeeds > GC_ERuleCheck.GREATER_THAN || m_eGroupNeeds == 8 || m_eGroupNeeds == 9')
s=s.replace('case GC_ERuleCheck.ODD_TRUE:\n\t\t\t\t\tanswer = (passed % 2) == 1;','case GC_ERuleCheck.LESS_THAN:\n\t\t\t\t\tanswer = passed < m_iHowMany;').replace('case GC_ERuleCheck.EVEN_TRUE:\n\t\t\t\t\tanswer = (passed % 2) == 0;','case GC_ERuleCheck.GREATER_THAN:\n\t\t\t\t\tanswer = passed > m_iHowMany;')
p.write_text(s,encoding='utf-8')
ops=[('Equal','Exactly this many are true (=)','Exactly this many listed conditions must be true.','EXACTLY',False),('NotEqual','A different number are true (!=)','The number of true conditions must differ from this count.','EXACTLY',True),('LessThan','Fewer than this many are true (<)','Fewer than this many listed conditions must be true. For 2, either 0 or 1 may be true.','LESS_THAN',False),('LessOrEqual','At most this many are true (<=)','No more than this many listed conditions may be true. For 2, either 0, 1 or 2 may be true.','AT_MOST',False),('GreaterThan','More than this many are true (>)','More than this many listed conditions must be true. For 2, at least 3 must be true.','GREATER_THAN',False),('GreaterOrEqual','At least this many are true (>=)','At least this many listed conditions must be true. For 2 out of 3, either 2 or 3 must be true.','AT_LEAST',False)]
s='//! Compare how many child conditions are true against a required count. Each child may be another group.\n\n'
for name,title,desc,kind,reverse in ops:
 s+=f'''[BaseContainerProps(), BaseContainerCustomStringTitleField("{title}")]
class GC_{name}Condition : GC_MissionCondition
{{
\t[Attribute("1", UIWidgets.SpinBox, "{desc}", "0 256 1")]
\tint m_iRequiredCount;
\t[Attribute("", UIWidgets.Object, "Conditions to count. Each entry contributes one true or false answer, even when it contains another group. This counts satisfied conditions, not people or entities.")]
\tref array<ref GC_MissionCondition> m_aConditions;

\t//------------------------------------------------------------------------------------------------
\toverride GC_RuleCondition BuildCheck(int depth = 0)
\t{{
\t\tif (!m_aConditions || m_aConditions.IsEmpty() || m_iRequiredCount < 0 || m_iRequiredCount > 256)
\t\t\treturn null;
\t\tGC_RuleCondition check = BuildGroup(m_aConditions, GC_ERuleCheck.{kind}, m_iRequiredCount, depth);
'''
 if reverse:s+='\t\tif (check)\n\t\t\tcheck.m_bReverseAnswer = true;\n'
 s+='\t\treturn check;\n\t}\n}\n\n'
(d/'GC_NumberConditions.c').write_text(s.rstrip()+'\n',encoding='utf-8')
# Replace the sample's old literal-number checks with a visible nested, two-out-of-three example.
p=r/'Prefabs/MP/Modes/GameMode_Lobby_NoFlagsExample.et';s=p.read_text();a=s.index('     m_aConditions {');b=s.index('     m_Event ',a)
s=s[:a]+'''     m_aConditions {
      GC_GreaterOrEqualCondition "{7B115D5DFF7EDA40}" {
       m_iRequiredCount 2
       m_aConditions {
        GC_AllConditions "{7B115D5DFF7EDA41}" {
         m_aConditions {
          GC_BooleanCondition "{7B115D5DFF7EDA44}" {
           m_bValue 1
          }
          GC_BooleanCondition "{7B115D5DFF7EDA45}" {
           m_bValue 1
          }
         }
        }
        GC_NoneConditions "{7B115D5DFF7EDA42}" {
         m_aConditions {
          GC_BooleanCondition "{7B115D5DFF7EDA43}" {
          }
         }
        }
        GC_BooleanCondition "{7B115D5DFF7EDA46}" {
        }
       }
      }
     }
'''+s[b:];p.write_text(s,encoding='utf-8')
p=r/'tests/McpTestAddon/scripts/Game/GC_MissionRuleTests.c';s=p.read_text().replace('rules[0].m_If.m_aChecks[0].m_aChecks.Count() == 2, "serialized nested numeric conditions are present"','rules[0].m_If.m_aChecks[0].m_aChecks.Count() == 3 && rules[0].m_If.m_aChecks[0].m_iHowMany == 2, "serialized two-out-of-three nested conditions are present"');p.write_text(s,encoding='utf-8')
