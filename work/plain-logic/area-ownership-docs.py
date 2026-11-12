from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8')
s=s.replace('Use **Area Condition** for named objects and **Living Characters** for living Players / AI. Both use the same sampler and shape checks. Area Condition has **Targets** (Named Entities), **Area**, **Inside / Outside**, and optional **Count**. Living Characters is configured directly in the mission event Conditions list, not inside a trigger.', 'Use **Area Condition** for named objects or living Players / AI inside/outside a shape. Use **Living Characters** for a living-population count anywhere in the mission. Area Condition has **Targets**, **Area**, **Inside / Outside**, and optional **Requirement** (Count or living-character Proportion). Both are ordinary framework conditions, with shared population sampling and no condition logic on the shape entity.')
s=s.replace('For living characters, use Living Characters with optional faction filtering and an optional Area.', 'For living characters in an area, choose Targets = Players / AI with optional faction filtering.')
s=s.replace('Choose **Targets > Named Entities** for exact world names. The previous **Targets > Players / AI** option is now hidden; use the standalone **Living Characters** condition below for new configurations. Saved Players / AI target objects still load and use the same runtime.', 'Choose **Targets > Named Entities** for exact world names, or **Targets > Players / AI** for eligible living characters. Both options are available directly on Area Condition.')
s=s.replace('The optional **Count** compares targets on the selected side', 'The optional **Requirement > Count** compares targets on the selected side')
s=s.replace('extraction proportions are provided by Living Characters below.', 'extraction proportions are provided by Area Condition below.')
a=s.index('### Living Characters: counts and extraction proportions')
s=s[:a]+'''### General living counts and area extraction

**Living Characters** is a general population condition. It has **Who** (Players, AI, or both), optional **Factions**, and optional **Requirement > Count**. It counts eligible living characters anywhere in the mission. There is no Area or Proportion option on this condition. Leave Requirement unset for Greater than 0. Count supports Greater than, Less than, Equal and Not equal.

**Area Condition** owns all area-specific settings. Choose **Targets > Players / AI**, set Who and Factions there, reference the named Radius/Polygon Area, select Inside or Outside, then choose **Requirement > Count** or **Proportion**. Leave Requirement unset to require at least one matching target (unless an older saved Count is present; see compatibility below). Named Entities supports Count only; a Proportion with Named Entities is rejected.

For extraction, use Area Condition: Targets = Players / AI, Who = Players, Factions = US, Area = ExtractionZone, Inside, Requirement = Proportion / 0.9. This passes when at least 90% of all currently living US players are inside. The denominator includes eligible living players outside too. Nine of ten passes; eight of nine fails; nine of nine passes. Deaths, respawns, disconnects and faction changes update the population on the next scheduled sample. Use Casualties to compare losses with the starting population instead.

Proportion 1 means everyone. An empty eligible population makes Proportion false, even at threshold 0. A nonempty population at threshold 0 passes regardless of location. A valid empty population has Count 0, so Count Equal 0 can pass. Outside uses the number outside and the same total. Missing/invalid areas or unavailable population data remain unavailable, including under None. Living Characters includes unconscious living characters and excludes spectators, dead characters and unoccupied playable slots, using the same existing filters as area population checks.

| Condition | Settings | Meaning |
| --- | --- | --- |
| Living Characters | Players; US; Count Less than 5 | Fewer than five living US players remain anywhere |
| Area Condition | Players / AI: Players, US; ExtractionZone / Inside; Proportion 0.9 | At least 90% of living US players are inside |
| Area Condition | Players / AI: AI, all factions; BaseArea / Inside; Count Equal 0 | No eligible living AI are inside |
| Area Condition | Named Entities: SupplyTruck; BaseArea / Inside | The named truck's origin is inside |

Neither condition has child conditions. Targets and Requirement contain settings only. Radius and Polygon entities supply reusable shapes only; events, flags and logical groups remain in the framework. Reuse the same named area across different checks. The shared population snapshot, membership cache and one-second schedule serve both general and area counts; no new discovery, polling loop, world scans, spawn hooks or prefab tracking were added. Count and Proportion for the same population/area share their observations, including changes to the denominator.

Compatibility: old Area Condition `m_Count GC_AreaCount` is retained as hidden serialized data and used when the new `m_Requirement` is unset. An explicit new Requirement overrides it. To migrate external resources, rename that field to `m_Requirement`; keep the count values. The existing local scene's old Count remains readable and its other edits were preserved.

The previous prototype's area-based Living Characters configuration must move to Area Condition: copy Who/Factions into `m_Targets GC_CharacterAreaTargets`, copy old Area Name to `m_sArea`, Side to `m_iCheck`, and keep the Count/Proportion object in `m_Requirement`. Living Characters now accepts Count only. A hidden legacy Area reader rejects old area-based checks with a migration diagnostic, so they cannot silently broaden to count everyone everywhere. No saved Living Characters entries were found in project resources during this change. Remove the legacy Area after migrating; do not merely change its name to blank.

Verification: production script compilation/reload and independent source review. Optional GC_LivingCharactersTests.c covers shared filters/observations, proportion boundaries, invalid combinations, missing context, and legacy-count precedence. Optional tests and in-game movement/respawn/multiplayer checks were not executed at the user's request. Existing unrelated Arland resource/menu diagnostics remain.
'''
p.write_text(s,encoding='utf-8')
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text(encoding='utf-8')
s=s.replace('Area Condition supports Named Entities. Living Characters is a separate framework leaf with Players / AI, optional factions and Area, and Count or Proportion requirements. Proportion measures eligible living characters on one side against the total living population; no eligible characters means false. Both conditions share the existing sampler. The older Players / AI area target remains hidden for saved-resource compatibility.', 'Area Condition supports Named Entities or Players / AI, with optional faction filters and Count or living-character Proportion requirements. Living Characters is a general living-population count without area settings. Proportion measures eligible living characters on one side against the total living population; no eligible characters means false. Both conditions share the existing sampler. Previous area-based Living Characters setups require migration to Area Condition; legacy area Counts remain readable.')
p.write_text(s,encoding='utf-8')
