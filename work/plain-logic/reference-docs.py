from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'scripts/Game/MissionFramework/GC_MissionRuleEngine.c';s=p.read_text().replace('m_aFlagRuns','m_aReactiveRuns');p.write_text(s)
p=r/'docs/MissionRules/README.md';s=p.read_text().replace('Flag State', 'Mission Flag Condition').replace('**Is On**','**Is true**').replace('**Is Off**','**Is false**').replace(' Is On',' Is true').replace('Is On/Is Off','Is true/Is false').replace('**Flag State / Is On**','**Mission Flag Condition / Is true**').replace('**Mission Flag Condition**, **Condition Group**','**Mission Flag Condition**, **Mission Event Condition**, **Condition Group**').replace('Mission events are not condition inputs yet.', 'Mission Event Condition can reference a named event\'s execution result.').replace('A rule containing a state or flag condition', 'A rule containing a state, flag or event-reference condition').replace('flag-dependent rules also check when a referenced flag changes.', 'flag-dependent rules also check when a referenced flag changes, and event-dependent rules when a referenced event runs.').replace('no state or flag conditions keeps', 'no state, flag or event-reference conditions keeps');i=s.index('## Game Mode State');s=s[:i]+'''## Referencing mission flags and mission events

**Mission Flag Condition** has Flag Name and Match (**Is true** / **Is false**). It reads the flag's current result. The conditions held inside the flag control its activation; referencing it does not evaluate those conditions again or call the flag. This replaces the former Flag State name, with the same true/false behavior. Counts stay in Condition Count.

**Mission Event Condition** has Event Name and the same Match choices. It reads whether the named event ran, without evaluating that event's conditions again or executing its actions:

- **Advanced > Run > Once**: starts false and remembers true after running.
- **Advanced > Run > Every time**: becomes true for the resulting check cycle, then silently resets to false. A later run sends another notification.

Events are checked at initial setup and relevant state/flag/event notifications, not by continuously polling every condition. An Is false reference can pass at initial setup, before the named event has run. Referenced events still run through their normal framework conditions, chance and delay. Scheduling a delayed event does not mark it true; executing it does. If actions queue flag calls, those calls are processed in the same notification cycle.

Event names must be exact and unique across regular events, random groups and their children. Missing, duplicate or invalid event names fail closed, even under Is false or a None group. An event with a disabled setting stays false until it runs (normally never). Empty conditions still mean true conditions, but do not make an event's execution result true before it runs.

A random group's result becomes true when it selects children. A selected child's result becomes true only if its own actions run; chance failure and pending delay do not count as a child run. A group with no eligible children does not publish a result.

These references nest inside Condition Group and Condition Count. Flags may also contain Mission Event Condition to activate in response to an event. A dependent event gets at most one eligible attempt per notification chain, avoiding repeatable feedback loops. Pulse resets do not send a second false notification. A delayed condition that requires a repeatable event to remain true will fail after that pulse resets; use a once-only source or a non-repeatable flag when the result must persist. Stopping clears pulses, and fresh startup clears all event results.

'''+s[i:];s+='''
### Named-reference migration

Rename saved GC_FlagStateCondition entries to GC_MissionFlagCondition; m_sFlagName and m_iMatch retain their values (0 true, 1 false). The project fixture and source callers are migrated. The new GC_MissionEventCondition uses m_sEventName and m_iMatch. This slice received production script compilation and source review only; no in-game tests or project restarts were performed.
''';p.write_text(s)
p=Path('work/mission-rules-cleanup/package.py');s=p.read_text().replace('Flag State and the combined Condition Count now support', 'Mission Flag Condition, Mission Event Condition and the combined Condition Count now support');p.write_text(s)
