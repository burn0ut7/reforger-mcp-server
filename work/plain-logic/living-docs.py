from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8')
s=s.replace('There is **one Area Condition** with **Targets**, **Area**, **Inside / Outside**, and optional **Count**. Targets chooses Named Entities or Players / AI, each with only its own settings.', 'Use **Area Condition** for named objects and **Living Characters** for living Players / AI. Both use the same sampler and shape checks. Area Condition has **Targets** (Named Entities), **Area**, **Inside / Outside**, and optional **Count**. Living Characters is configured directly in the mission event Conditions list, not inside a trigger.')
s=s.replace('Named Entities accepts a list; Players / AI selects living characters with optional faction filtering. Optional Count compares matching targets.', 'Named Entities accepts a list. For living characters, use Living Characters with optional faction filtering and an optional Area. Count compares matching targets; Proportion compares eligible living characters on one side with all eligible living characters.')
s=s.replace('Choose **Targets > Named Entities** for exact world names, or **Targets > Players / AI** for living characters.', 'Choose **Targets > Named Entities** for exact world names. The previous **Targets > Players / AI** option is now hidden; use the standalone **Living Characters** condition below for new configurations. Saved Players / AI target objects still load and use the same runtime.')
s += '''

### Living Characters: counts and extraction proportions

Add **Living Characters** directly to a mission event's Conditions list. It is one leaf condition: it has no Conditions array and cannot contain flags, triggers or other conditions. A normal Condition Group can combine it with other framework conditions when needed. Radius and Polygon areas remain passive shape definitions, with no mission logic on them.

- **Who:** Players, AI, or Players and AI. Only living characters count. Unconscious living characters are included; spectators, dead characters and empty playable slots are excluded. Players are currently controlled characters of connected players. AI uses the existing registered-agent and playable-slot exclusions.
- **Factions:** optional faction keys. Empty means all factions; selected factions are combined and duplicate entries count once.
- **Area:** optional, shallow location settings (Name and Inside/Outside). Leave unset for Anywhere. An added Area must name a valid radius or polygon; an empty name is an invalid configuration, not Anywhere.
- **Requirement:** optional, shallow Count or Proportion settings. Leave unset for Count greater than 0. Count offers Greater than, Less than, Equal and Not equal. Proportion contains only Required Proportion (0..1), and requires Area. Selecting Proportion without Area is invalid.

There are no child conditions inside Area or Requirement. These two settings objects keep unused fields out of the editor: no area name for Anywhere, no numeric count threshold on Proportion, and no proportion field on Count.

For extraction, choose Players, the desired factions, Area = ExtractionZone / Inside, and Requirement = Proportion / 0.9. This passes when at least 90% of all currently living characters matching Who and Factions are inside. The denominator includes those outside too. Nine of ten passes; eight of nine fails; nine of nine passes. Deaths, respawns, disconnects and faction changes affect the current population on the next scheduled sample. This does not measure survival against a starting population: use Casualties for that.

An empty eligible population makes Proportion false, including at threshold 0. A nonempty population at threshold 0 passes regardless of location. A valid empty population has Count 0, so Count Equal 0 can pass. Missing/invalid areas or unavailable population data remain unavailable and cannot pass even inside None. Outside uses the number outside and the same total; it does not mean everybody must be outside.

Examples:

| Condition | Settings | Meaning |
| --- | --- | --- |
| Living Characters | Players; US; Area unset; Count Less than 5 | Fewer than five living US players remain |
| Living Characters | Players; US; ExtractionZone / Inside; Proportion 0.9 | At least 90% of living US players are inside |
| Living Characters | AI; all factions; BaseArea / Inside; Count Equal 0 | No eligible living AI are inside |
| Area Condition | Named Entities = SupplyTruck; BaseArea / Inside | The named truck's origin is inside |

Runtime: the existing active-server Lobby2 sampler owns all observations. Anywhere does not resolve or create a shape. Count and Proportion with the same population filters and area share their counts; different sides and thresholds reuse those counts. A denominator change is included when deciding whether the answer changed. Population discovery occurs once per sample, AI discovery only when needed, and radius/polygon membership is cached per entity/area. Notifications are emitted on answer or availability changes, with existing startup/phase checks. No new polling loop, world scan, vehicle registration, spawn hook or prefab discovery was introduced.

Compatibility: existing GC_AreaCondition + GC_CharacterAreaTargets resources remain readable through a hidden, noninsertable legacy target class. It delegates population filter construction to Living Characters and uses the same sampler. For migration, create GC_LivingCharactersCondition; copy Who/Factions from the old target, put the old area name and side into Area, and move Count into Requirement. Leave Requirement unset if Count was unset. No old serialized values are silently reinterpreted. No configured legacy Players / AI target entries were found in saved project resources before this change; unrelated scene edits were preserved.

Verification: production scripts compiled and reloaded successfully, and independent source review checks logic, sharing and ownership. Optional GC_LivingCharactersTests.c covers authoring validation, count/proportion observation sharing, ratio boundaries and unavailable data under None. These optional tests and in-game movement/respawn/multiplayer behavior were not executed, as requested. Existing unrelated Arland resource/menu diagnostics remain.
'''
p.write_text(s,encoding='utf-8')
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text(encoding='utf-8')
s=s.replace("'tests/McpTestAddon/scripts/Game/GC_CasualtyConditionTests.c',", "'tests/McpTestAddon/scripts/Game/GC_CasualtyConditionTests.c','tests/McpTestAddon/scripts/Game/GC_LivingCharactersTests.c',",1)
s=s.replace('Area targets support Named Entities or Players / AI with optional faction filters and an optional Count comparison.', 'Area Condition supports Named Entities. Living Characters is a separate framework leaf with Players / AI, optional factions and Area, and Count or Proportion requirements. Proportion measures eligible living characters on one side against the total living population; no eligible characters means false. Both conditions share the existing sampler. The older Players / AI area target remains hidden for saved-resource compatibility.')
p.write_text(s,encoding='utf-8')
