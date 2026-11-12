from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/docs/MissionRules/README.md');s=p.read_text().replace('The preceding GC_MissionCondition + m_iType format also changed: All/Any become their list objects, and None becomes the None group.', 'The preceding GC_MissionCondition + m_iType format also changed: recreate groups using Condition Group and its Match setting.')
a=s.index('### Updating the preceding condition-object slice');s=s[:a]+'''### Migrating saved conditions to this version

The editor now uses two composite types:

- **GC_ConditionGroup**: m_iMatch (0 All, 1 Any, 2 None, 3 Not all) and m_aConditions. No number field.
- **GC_ConditionCount**: m_iComparison (0 Greater than, 1 Less than, 2 Equal to), m_iRequiredCount and m_aConditions.

Replace the former All/Any/None/NotAll classes with Condition Group and the matching mode, keeping their children. An old GC_NotCondition becomes a None group with its former m_Condition as the sole child.

Replace separate comparison classes with Condition Count. For integer counts, >= N can become > N-1 when N is positive; <= N can become < N+1 when the new threshold remains within 0–256. Boundary cases require a deliberate equivalent group; do not enter an invalid threshold. Not-equal can be expressed as a None group containing an Equal to count. Old fixed m_fFirstNumber/m_fSecondNumber comparisons need deliberate rebuilding; they do not represent live checks automatically.

The shipped two-of-three example now uses Greater than 1 and combined All/None groups. The user's empty Greater than object was renamed without changing its meaning. External resources need the same conversions before use. Removed XOR/XNOR classes require deliberate replacement; internal values 8 and 9 are rejected rather than reused. Internal legacy count modes remain for historical interpreter behavior, not as editor choices.

Flag On/Off authoring classes remain removed; external missions using them need a deliberate update when special conditions return. No flag check is silently converted to a fixed value.
''';p.write_text(s)
