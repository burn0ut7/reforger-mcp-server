from pathlib import Path
p=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2/docs/MissionRules/README.md')
s=p.read_text()
s=s.replace('Add a **Mission Event**','Add a **GC_Event**').replace('Leave **Advanced** and **Random** empty unless needed.','Leave **Advanced** empty unless needed.').replace('Name, Conditions, Actions, Advanced, Random','Name, Conditions, Actions, Advanced')
a=s.index('## Optional randomness'); b=s.index('## Included examples',a)
s=s[:a]+'''## Random chance and random groups

**Advanced > Random Chance** is a number from 0 to 1: **0.8 means 80%**, **1 means 100%**, and **0 means never**. The default is 1. Leaving Advanced unset or omitting the saved chance value means 100%. Chance controls whether actions run; it only spawns something if an action does so. The old separate Random object is removed.

Chance is checked once after conditions pass, before the wait. A failed chance waits for a new matching state change; it is not retried every tick. Conditions are checked again after the wait.

In **Mission Events**, choose either **GC_Event** or **GC_RandomEventGroup**:

- **GC_Event**: Name, Conditions, Actions, Advanced.
- **GC_RandomEventGroup**: Name, Conditions, Pick Count, Events, Advanced.

A random group chooses the requested number of different child events whose conditions currently pass. Disabled, waiting and completed once-only children are excluded. If fewer qualify, it picks those available. It never picks the same list entry twice in one selection. If none qualify, a once-only group stays available for a later matching state change.

The group's conditions decide when selection happens. Child conditions filter that selection; they do not trigger the group independently. Each selected child's own chance and delay still apply, so Pick Count is the number selected, not a guarantee that that many actions execute. A child that fails its chance is not replaced by another pick.

The group's delay happens before selection; child delays happen afterward. Waiting children must continue to satisfy both their own and their parent's conditions. A once-only group uses its one selection even if a selected child's chance fails or its later wait is canceled. Set the group's Run to Every time for later matching state changes; children retain their own Once/Every time settings.

Groups contain regular events, not other random groups. Conditions still nest normally. No weighted selection or full gameplay catalog is included. Random flag and Select random flags remain optional test actions; flags are not needed for event selection.

'''+s[b:]
s=s.replace('starts a fixed two-second wait plus a random extra 1-3 seconds, after which one of two test log messages runs once.','starts a random group with a fixed two-second wait and Pick Count 1, selecting one of two regular test-log events. There is no extra random delay.')
s=s.replace('Limits are 256 mission events, initial flags, actions, or children per group; nesting is limited to 16.','The total event limit is 256, including random groups and their children. Each random group holds up to 255 regular events, with Pick Count from 1 to its configured child count. Initial flags, actions and condition lists are limited to 256; condition nesting is limited to 16.')
s=s.replace('479 checks passed (85 state','508 checks passed (29 event-group/chance, 85 state')
s=s.replace('This framework also supports direct random action selection without flags and event-based random delays.','This framework supports random selection of regular events without flags.')
s=s.replace('Internal class names remain unchanged.','The regular authoring class is now GC_Event; internal interpreter names retain MissionRule.')
s=s.replace('preserving its once-only delayed random action without Boolean placeholders.','using a once-only random group without Boolean placeholders.')
s+='''
### Migrating regular events and randomness

Rename saved GC_MissionRule objects to **GC_Event** and remove obsolete GC_MissionRule.c when installing a source snapshot. The Mission Events list now accepts GC_Event and GC_RandomEventGroup through its shared serialized base GC_EventDefinition. Incomplete base entries fail closed; choose a concrete event type if a picker exposes the base.

Move old Random > Chance Percent into Advanced > Random Chance, dividing by 100. Remove m_Random from authored events. Convert One random action into a random group with Pick Count 1 and one regular child event per former action. Old additional random delays require deliberate redesign; they are not silently preserved. The shipped no-flags example deliberately changes its former fixed-plus-random delay to a fixed two seconds. Historical random behavior remains available only to compiled interpreter callers and regression tests.

This slice's live checks include saved example deserialization, default chance when omitted, chance boundaries, filtering, distinct picks, insufficient eligible children, delays, cancellation and repeat behavior. Full native object-class picker options and dedicated/remote-client behavior were not newly verified.
'''
p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py'); s=p.read_text(); s=s.replace("'tests/McpTestAddon/scripts/Game/GC_GameModeConditionTests.c',", "'tests/McpTestAddon/scripts/Game/GC_GameModeConditionTests.c','tests/McpTestAddon/scripts/Game/GC_EventGroupTests.c',")
s=s.replace('One Mission Event type,','Regular GC_Event and Random Event Group types,').replace('optional chance/random delay/random action selection','Advanced Random Chance (0..1, default 1), distinct eligible-child selection').replace('479 checks','508 checks').replace('GC_MissionEvent.c, GC_RuleDefinition.c','GC_MissionEvent.c, GC_MissionRule.c, GC_RuleDefinition.c').replace("('GC_MissionEvent.c','GC_RuleDefinition.c'", "('GC_MissionEvent.c','GC_MissionRule.c','GC_RuleDefinition.c'")
p.write_text(s)
