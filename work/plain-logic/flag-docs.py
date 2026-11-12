from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'docs/MissionRules/README.md';s=p.read_text();a=s.index('**Flags are optional.**');b=s.index('## Game Mode State',a);s=s[:a]+'''**Flags are optional.** Use them when a mission needs to remember something or notify other events. Each declared Mission Flag remembers its On/Off state and the number of accepted calls.

## Mission Flags: memory and calls

A Mission Flag has **Name**, **Starting Value**, **Repeatable**, and **Conditions**. Use the **Call Flag** action with the same exact name.

- **Repeatable off** (default): accepts one call, becomes On and stays On. Later calls do nothing.
- **Repeatable on**: becomes On while dependent events and flags are checked, then resets to Off. Its count is remembered, so a later call can activate it again.
- **Conditions**: requirements for accepting a call. No conditions means always true. Conditions are checked before increasing the count; blocked calls do not count or notify anything.

For **at most five activations**, name the flag `wave`, turn on Repeatable, and add **Flag Call Count** to its Conditions: Flag Name `wave`, Comparison **Less than**, Count **5**. Counts 0 through 4 allow a call; each accepted call adds one. The fifth succeeds, and later calls are blocked.

Mission events and flags can use two condition objects, nested inside the usual groups:

| Condition | Meaning |
| --- | --- |
| Flag State | Check whether a named flag is On or Off. |
| Flag Call Count | Compare its remembered accepted-call count using Greater than, Less than or Equal to. |

**Flag Call Count** counts activations. **Condition Count** counts how many child conditions are true. They serve different purposes.

When a flag changes, events and flags that reference it are checked. A flag's conditions can therefore form a chain: `second` with Flag State `first` Is On activates when `first` is called. A flag checking only its own count does not call itself; that condition limits incoming calls. A Game Mode State condition on a flag also lets a matching startup/state change call it. No conditions means no automatic trigger: call the flag with an action.

Each flag can activate at most once in one check cycle, and each eligible dependent mission event gets one attempt in that cycle. This prevents circular chains from repeatedly firing. A later external call starts a fresh cycle. Repeatable flags remain On throughout the chain, then reset together; that reset does not trigger another cycle. Mission events still respect their own Once/Every time, chance and delay settings. Use **Every time** on an event that should react to successive calls.

For delayed actions, conditions are rechecked when the wait ends. A repeatable flag will already be Off then, so a delayed **Flag State / Is On** condition will fail. Use its remembered **Flag Call Count** when the requirement must survive the brief pulse.

Starting Value supplies initial memory without adding a count or sending a notification. A non-repeatable flag starting On is already latched. All counts start at zero and reset on fresh framework startup. Flag names are case-sensitive. Unknown names read as Off/count zero, but Call Flag requires a declared Mission Flag. Invalid/duplicate definitions prevent startup.

For compatibility, **Set flag / On** calls declared flags through the same conditions/count/repeat policy. **Off** clears the state without resetting the count or rearming a used non-repeatable flag. Undeclared Set flag names retain the earlier simple On/Off behavior. Random flag actions use that same Set flag policy. Prefer Call Flag for the new workflow.

'''+s[b:]
s=s.replace('A rule containing a state condition is checked on startup and every distinct state change.', 'A rule containing a state or flag condition is checked on startup and every distinct state change; flag-dependent rules also check when a referenced flag changes.')
s=s.replace('| Flag | An optional named On/Off value the mission remembers. |','| Flag | Optional memory: On/Off state and an accepted-call count. |')
s=s.replace('**Game Mode State**, **Condition Group**, or **Condition Count**','**Game Mode State**, **Flag State**, **Flag Call Count**, **Condition Group**, or **Condition Count**')
s=s.replace('Flag On/Off condition objects have been removed from this authoring slice. Area, faction, prefab, flag and timer conditions remain future slices. Mission Flags and flag actions remain available; timing and randomness have not moved into conditions yet.', 'Flag State is one condition object with Is On/Is Off choices. Area, faction, prefab and timer conditions remain future slices; timing and randomness have not moved into conditions yet.')
s=s.replace('Existing actions are Log message, Set flag, Send test event, Random flag, and Select random flags.', 'Existing actions are Log message, Call Flag, Set flag, Send test event, Random flag, and Select random flags.')
s=s.replace('Old On/Off flag checks require a deliberate update when special conditions return; they are not replaced with fixed values.', 'Old separate On/Off flag checks can be recreated using Flag State and its Is On/Is Off choice.')
s=s.replace('Flag On/Off authoring classes remain removed; external missions using them need a deliberate update when special conditions return. No flag check is silently converted to a fixed value.', 'The former separate Flag On/Off classes remain removed. Recreate them with the single Flag State condition and the matching choice.')
s=s.replace('509 checks passed (30 event-group/chance', '560 checks passed (48 flag memory, three serialized flag defaults, 30 event-group/chance')
s=s.replace('The temporary test configuration was discarded by stopping Play.', 'The production manager CallFlag path also passed count/pulse and disabled/shutdown/stopped rejection checks. The temporary test configuration was discarded by stopping Play.')
s+='''
### Flag-memory migration and example

Existing declared Mission Flags default to non-repeatable. Set flag / On now respects their conditions and counts an accepted call; it no longer bypasses declared policy. If an older mission repeatedly toggles a declared flag, turn on Repeatable and account for its brief On pulse, or deliberately retain an undeclared simple memory name. Clearing a non-repeatable flag does not rearm it. No old external mission is silently assumed to need repeatable behavior.

`tests/McpTestAddon/Prefabs/FlagMemoryExample.et` is an optional editor/test fixture. It demonstrates a repeatable `wave` flag limited to five calls and a regular event reacting on every accepted call. A second `remember` flag demonstrates the default non-repeatable setting. It is not installed into the user's Arland scene.

Server extensions can call GC_LobbyManager.CallFlag(name) and read GetFlagCallCount(name). Calls require the active authoritative Lobby2 owner, an enabled framework and a running engine. This adds no client RPC. True from CallFlag means queued; a call queued by another action is checked again when processed. Counts are server-local, not a replicated general-purpose state API. The existing bounded queue still stops oversized event chains.
''';p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_Event.c';s=p.read_text().replace('Every time allows later matching state changes, not repeated execution while a state stays the same.', 'Every time allows later matching state changes or flag calls. It does not run continuously while conditions remain true.');p.write_text(s)
p=r/'scripts/Game/MissionFramework/GC_FlagConditions.c';s=p.read_text().replace('The number of calls to compare against. This counts calls, not how many child conditions pass.', 'The number of accepted calls to compare against. On the flag itself, Less than 5 allows five calls. Blocked calls do not count.');p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace("'tests/McpTestAddon/scripts/Game/GC_EventGroupTests.c',", "'tests/McpTestAddon/scripts/Game/GC_EventGroupTests.c','tests/McpTestAddon/scripts/Game/GC_FlagMemoryTests.c','tests/McpTestAddon/Prefabs/FlagMemoryExample.et','tests/McpTestAddon/Prefabs/FlagMemoryExample.et.meta',");s=s.replace('509 checks','560 checks').replace('Flags as special conditions and live numeric sources are deferred.', 'Flag State and Flag Call Count now support remembered calls, conditions and repeatable pulses; live entity numeric sources remain deferred.').replace('timer/flag/named-event conditions', 'timer/named-event conditions');p.write_text(s)
