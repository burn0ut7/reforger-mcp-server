from pathlib import Path
r=Path(r'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/addons/Lobby2')
p=r/'docs/MissionRules/README.md';s=p.read_text(encoding='utf-8');a=s.index('1. Add a **Mission Event**');b=s.index('## Conditions: standard logic objects',a)
s=s[:a]+'''1. Add a **Mission Event** and give it a name.
2. In **Conditions**, add **Game Mode State**. Choose a phase and **Is in this state** or **Is not in this state**.
3. Add **Actions**. They normally run in order.
4. Leave **Advanced** and **Random** empty unless needed.

For game start, choose **Match / Is in this state**. There is no separate Event object or event-type selector.

A mission event runs once by default. **Advanced > Run > Every time** permits a new run on each later matching state change. Staying in the same state does not repeat it. **Wait Seconds** adds a delay; Disabled keeps the event saved without running it.

**Flags are optional.** They are named On/Off values remembered by the mission, not prerequisites for state-based events. Mission Flags supplies optional starting values before the first condition check. Names are exact and case-sensitive; undeclared flags start Off. Invalid or duplicate initial flags prevent startup. Setting flags does not recheck Game Mode State conditions.

## Game Mode State

Choose **Mission preview**, **Role selection**, **Briefing**, **Match**, or **After action**. These are the existing authoritative Lobby2 phases; this feature does not introduce another phase system.

- **Is in this state** passes when that phase is active.
- **Is not in this state** passes when a different valid phase is active.

The framework checks the initial state after the world loads, then listens through GC_LobbyManager's existing OnGameStateChanged callback. Duplicate notifications and ordinary ticks do not trigger another check. A late startup observes the current state; it does not invent a transition. The initial state can satisfy an Is not in condition too.

State conditions can appear anywhere inside Condition Group or Condition Count. A rule containing a state condition is checked on startup and every distinct state change. All top-level conditions must pass. A rule with no state conditions keeps the simple default: check once when Match begins.

Actions waiting on a delay are rechecked when due. A state change that makes their conditions false cancels the pending run. If the conditions remain true, the original wait continues. Explicit After action conditions and their delays may run after the match; shutdown cancels everything. Conditions with no valid state context fail closed, even inside None.

## The terms

| Setting | Meaning |
| --- | --- |
| Mission Event | A name, conditions, and actions to run when the conditions pass. |
| Condition | A requirement checked on relevant changes, and again after a wait. |
| Action | Something the mission event does. |
| Flag | An optional named On/Off value the mission remembers. |

'''+s[b:]
s=s.replace('Use **+** to choose **Condition Group** or **Condition Count**. Both contain their own Conditions list,','Use **+** to choose **Game Mode State**, **Condition Group**, or **Condition Count**. Group and Count contain their own Conditions list,').replace('Until special checks arrive, leave Conditions empty for unrestricted events; empty explicit groups still fail closed.','Game Mode State is the first live check. Empty explicit groups still fail closed.').replace('Special conditions and live value sources will be added later. Existing Mission Flags, flag actions and starting-event options remain separate. Moving events, timing and randomness into conditions is also a future slice.','Area, faction, prefab, flag and timer conditions remain future slices. Mission Flags and flag actions remain available; timing and randomness have not moved into conditions yet.')
s=s.replace('Events received while waiting are ignored. Repeat timers skip missed ticks instead of executing a burst.','Matching state changes while waiting do not restart the delay.').replace('Use Match started plus 50% chance for an uncertain opening event; Named event received plus a random delay for a response to an earlier event; or a repeating Timer with a chance for occasional activity. Use flags only when later mission events must remember or react to a selected outcome.','Use Match / Is in this state plus 50% chance for an uncertain opening event, or Briefing / Is in this state plus a random delay for activity during briefing. A failed chance is not retried every tick. Use flags only when a result needs to be remembered.')
s=s.replace('no flags and no conditions; it demonstrates an unrestricted timer event. After a two-second timer and a random extra 1-3 seconds,','no flags; Match / Is in this state starts a fixed two-second wait plus a random extra 1-3 seconds, after which').replace('no-flags timer example','no-flags state example').replace('two mission events connected by a flag. First sets test_ready; second observes that change and sets test_complete.','two independent state-driven mission events. Match sets test_ready; After action sets test_complete. They demonstrate optional flag actions, not flag-triggered events.')
s=s.replace('Named events can be sent by the sample action or the server-side manager SendTestEvent method. No RPC or arbitrary script execution is exposed.','Send test event remains an interpreter/testing action; this slice has no authorable named-event condition to receive it. No RPC or arbitrary script execution is exposed.')
s=s.replace('Only the authoritative active Lobby2 game mode starts mission events, after briefing at GAME. Shutdown and POSTGAME cancel pending work.','Only the authoritative active Lobby2 game mode starts the framework, after world load, and supplies state observations. It uses the existing component callback with no additional global subscription. Shutdown and loss of active mode ownership stop pending work. Explicit After action conditions can run in POSTGAME; old non-state delayed/timer work is canceled there.')
a=s.index('Workbench 1.9.0.37:');b=s.index('\n\n',a)
s=s[:a]+'''Workbench 1.9.0.37: 479 checks passed (85 state conditions, 189 standard logic, 148 interpreter, 22 authoring, six no-flags resource, four state-example resource, five historical-fixture and 20 randomness checks). Real Lobby2 transitions were also tested through Preview -> Role selection -> Briefing -> Match -> After action: each action fired once, duplicate notifications were ignored, unauthenticated phase changes were rejected, and stopping prevented further execution. The temporary test configuration was discarded by stopping Play. Dedicated/remote-client behavior was not newly tested. Known unrelated Arland GUID and missing menu-preset diagnostics remain.

Installed engine source: game/data/data007.pak/scripts/Game/GameMode/SCR_BaseGameMode.c (SHA256 c02bf603991e03e956d880ffaf643cfbde46f87a7d31f2fb1fc0918e95c16652), lines 434-465, dispatches OnGameStateChanged to components on server and clients. The manager gates mission execution to the active authoritative Lobby2 owner.''' +s[b:]
s=s.replace('The shipped no-flags example now has no conditions, preserving its timer and random action behavior without fixed Boolean placeholders.','The shipped no-flags example now uses a Match state condition and a fixed wait, preserving its once-only delayed random action without Boolean placeholders.')
s+='''
### Migrating the former Event selector

Remove the obsolete GC_MissionEvent.c file when installing a source snapshot. GC_MissionRule.m_Event and the GC_MissionEvent authoring type are removed. Match started becomes a Game Mode State condition: m_eGameModeState = 1 (GAME), m_iMatch = 0 (Is in). Invert uses m_iMatch = 1. The saved empty Match-start selector in the inspected Arland scene was converted.

A once-only Timer after match start can use Match / Is in with Advanced > Wait Seconds; this is how the no-flags example is migrated. Repeating timers, named-event receivers and flag-change triggers need deliberate migration when those conditions are added; they are not silently converted to unconditional events. The old two-event flag-chain sample is deliberately replaced with independent Match and After action examples. The compiled interpreter still supports its historical triggers for regression/extension callers, but they are no longer editor options.
''';p.write_text(s,encoding='utf-8')
p=r/'docs/Lobby2_Architecture_and_Best_Practices.md';s=p.read_text(encoding='utf-8');s+='''
## Mission framework state conditions

GC_LobbyManager starts its server-local mission engine from OnWorldPostProcess, observing the active Lobby2 phase. Its existing OnGameStateChanged callback supplies subsequent distinct states; there is no additional global listener or phase authority. Authored Game Mode State leaves compose through Condition Group/Count. Conditions are checked on initial observation and phase changes, with delayed actions revalidated. The framework remains alive through POSTGAME for explicit After action events and stops on shutdown or loss of mode ownership. Engine state is an observation used for evaluation, not a replicated phase store. Historical interpreter triggers remain internal; the editor has no separate Event selector.
''';p.write_text(s,encoding='utf-8')
