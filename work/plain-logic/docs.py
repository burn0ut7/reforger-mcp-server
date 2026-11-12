from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/docs/MissionRules/README.md')
s=p.read_text();a=s.index('| All conditions');b=s.index('Every condition directly',a)
s=s[:a]+'''| All are true (AND) | Every child must be true. |
| At least one is true (OR) | One or more children must be true. |
| None are true (NOR) | Every child must be false. With one child, this reverses its answer. |
| At least one is false (NAND) | Passes unless every child is true. |

**None replaces Not.** Any group can contain other groups. XOR and XNOR have been removed.

Count comparisons have **Required Count** and their own **Conditions** list:

| Condition object | Meaning |
| --- | --- |
| Exactly this many are true (=) | The number of true children equals Required Count. |
| A different number are true (!=) | The number differs from Required Count. |
| Fewer than this many are true (<) | The number is below Required Count. |
| At most this many are true (<=) | The number is no greater than Required Count. |
| More than this many are true (>) | The number exceeds Required Count. |
| At least this many are true (>=) | The number reaches or exceeds Required Count. |

For **two out of three**, add **At least this many are true**, set Required Count to **2**, and add three conditions. Each child can itself be an All, None, another count comparison, or any other condition group. Each child contributes one true-or-false answer, regardless of how many conditions it contains.

For example, **At least 2** can contain **All [A, B]**, **None [C]**, and **D**. It passes when two or three of those branches pass. This is the nested layout demonstrated by the no-flags example. Another valid layout is **Any [All [A, B], All [C, D]]**.

These comparisons count **satisfied conditions**, not people or entities. Future area, faction-survival and prefab checks will decide their own true-or-false answers before the enclosing group counts them. Mission events are not condition inputs yet. A **True or false value** supplies a fixed input for testing: checked = true, unchecked = false. It is not a flag or a live check.

'''+s[b:]
s=s.replace('invalid numeric inputs','invalid required counts').replace('nested All/None and numeric comparisons','a two-out-of-three comparison containing All/None groups').replace('nested numeric example','nested count example').replace('Specialized count conditions are deferred; they are not silently converted.','Count comparisons now count satisfied child conditions.').replace('Workbench 1.9.0.37: this slice passed 309 checks: 148 interpreter, 30 authoring, 121 standard-logic, four serialized flag-action example checks, and six serialized nested-logic example checks. Standard-logic checks cover every one-, two- and three-input Boolean truth table, numeric comparison outcomes, empty/malformed groups, cycles, mixed nesting and production execution.','Workbench verification for this update is recorded after the live test pass. Tests cover Boolean truth tables, count comparison boundaries, empty/malformed groups, cycles, nested groups and production execution.')
s+='''
### Updating the count-comparison slice

The six comparison classes now use m_iRequiredCount and m_aConditions instead of fixed m_fFirstNumber/m_fSecondNumber values. Rebuild external fixed-number comparisons deliberately; those values cannot be converted into live checks automatically. Empty comparison groups fail closed. The project example was migrated; the saved Arland comparison had no explicit number values. Removed XOR/XNOR classes require deliberate replacement in external resources; old internal mode values 8 and 9 are rejected rather than reused.
'''
p.write_text(s)
