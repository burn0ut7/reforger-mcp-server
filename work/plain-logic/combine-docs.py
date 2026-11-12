from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/docs/MissionRules/README.md');s=p.read_text();a=s.index('Count comparisons have');b=s.index('These comparisons count',a)
s=s[:a]+'''Add one **Condition Count** object for number comparisons. It has three settings:

1. **Comparison**: Greater than (>), Less than (<), or Equal to (=).
2. **Required Count**: the number to compare against.
3. **Conditions**: the checks being counted. Each can contain nested groups.

| Comparison with Required Count = 2 | Passes when |
| --- | --- |
| Greater than | 3 or more listed conditions are true. |
| Less than | 0 or 1 listed conditions are true. |
| Equal to | Exactly 2 listed conditions are true. |

For **at least two out of three**, use **Greater than**, set Required Count to **1**, and add three conditions. Each child contributes one true-or-false answer, regardless of how many conditions it contains. For exactly two, choose Equal to and set the count to 2.

For example, **Greater than 1** can contain **All [A, B]**, **None [C]**, and **D**. It passes when two or three of those branches pass. This is the nested layout demonstrated by the no-flags example. Another valid layout is **Any [All [A, B], All [C, D]]**.

'''+s[b:];s=s.replace('439 checks passed with zero failures (251 standard logic','375 checks passed with zero failures (187 standard logic')
s+='''
### Updating to the combined Condition Count

The six separate comparison classes are replaced by GC_ConditionCount, with m_iComparison (0 Greater than, 1 Less than, 2 Equal to), m_iRequiredCount and m_aConditions. Greater-than-or-equal and less-than-or-equal are no longer authoring choices. Not-equal can be expressed as None containing an Equal to count. External resources must replace the old comparison objects deliberately. The shipped two-of-three example now uses Greater than 1; the user's empty Greater than object was renamed without changing its meaning. Internal legacy interpreter modes remain for historical behavior, not as editor choices.
''';p.write_text(s)
