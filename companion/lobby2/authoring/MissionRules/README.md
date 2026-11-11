# Lobby2 mission events

Area Condition supports capture timing, competing faction shares, interruption
behavior and optional status messages. See [Area capture](Capture.md).

Select your game mode, then **GC_LobbyManager > Lobby2**. The top settings are **Mission Events**, **Mission Flags**, **Briefing Catalog**, and **Parameter Catalog**. Mission Events Enabled and Parameter Lock Seconds remain in Unsorted.

## Start with one mission event

1. Add a **GC_Event** and give it a name.
2. In **Conditions**, add **Game Mode State**. Choose a phase. Leave **Require Other State** unchecked to match it; check it to match other phases.
3. Add **Actions**. They normally run in order.
4. Leave the **Advanced** category at its defaults unless needed.

For game start, choose **Match** with **Require Other State** unchecked. Name, Conditions and Actions remain event settings; additional controls are grouped under Advanced. There is no separate Event object or event-type selector.

A mission event runs once by default. **Advanced > Repeatable (checked)** permits a new run on each later matching state change. Staying in the same state does not repeat it. **Delay** adds a wait measured in seconds; Disabled keeps the event saved without running it.

**Flags are optional.** Use them when a mission needs to remember something or notify other events. Each declared Mission Flag holds a remembered call count; zero means false and a positive count means true. Starting values and Flag Action count edits are included.

## Mission Flags: memory and calls

A Mission Flag has **Name**, **Start Call Count**, **Repeatable**, and **Conditions**. Its value is determined by its remembered call count: **zero = false; one or more = true**. Start Call Count defaults to zero; a positive value starts the flag true. Checking a flag does not call it. Use **Flag Action > Call** with the same exact name.

- **Repeatable unchecked** (default): accepts one call, becomes true and stays true. Later calls do nothing until Reset Flag is used.
- **Repeatable checked**: accepts later calls, increasing its count and notifying dependent events each time. It stays true until Reset Flag clears the count.
- **Conditions**: requirements for accepting a call. No conditions means a call has no extra requirements, not that the flag is already true. Conditions are checked before increasing the count; blocked calls do not count or notify anything.

For **at most five activations**, name the flag `wave`, check Repeatable, and add **Count Condition** to its Conditions: **Count Flag Calls** checked, Flag Name `wave`, Comparison **Less than**, Required Count **5**. Counts 0 through 4 allow a call; each accepted call adds one. The fifth succeeds, and later calls are blocked.

Mission events and flags can use two condition objects, nested inside the usual groups:

| Condition | Meaning |
| --- | --- |
| Mission Flag Condition | Check whether a named flag is true or false. |
| Count Condition | Count passing conditions or read a named flag's remembered count, then compare using Greater than, Less than, Equal or Not equal. |

**Count Condition** handles both uses. Check **Count Flag Calls** to count flag calls; leave it unchecked to count true child conditions. There is no separate Flag Call Count condition.

When a flag changes, events and flags that reference it are checked. A flag's conditions can therefore form a chain: `second` with Mission Flag Condition `first` with Require False unchecked activates when `first` is called. A flag checking only its own count does not call itself; that condition limits incoming calls. A Game Mode State condition on a flag also lets a matching startup/state change call it. No conditions means no automatic trigger: call the flag with an action.

Each flag can activate at most once in one notification chain, and each eligible dependent event gets one attempt in that chain. This prevents circular chains from repeatedly firing. A later external call starts a fresh chain. Repeatable flags retain their value and count between chains. Mission events still respect their own Repeatable, chance and delay settings.

**Reset Flag** clears successful calls to zero, making the flag false and allowing a non-repeatable flag to accept a later call. It notifies observers if the value/count changed; resetting an already-zero or unknown flag is a no-op. Blank names are rejected. Reset preserves the current chain's call guard, so call-reset-call cannot repeatedly activate the same flag in one chain. Reset does not clear mission event results or their run limits. Automatic dependent flag conditions still apply to notifications, so reset can trigger other configured behavior.

For delayed actions, conditions are rechecked when the delay ends. Flag memory remains true after an accepted call, including for repeatable flags, unless Reset Flag clears it. Conditions on the flag govern acceptance of future calls; becoming false later does not undo earlier calls.

Fresh framework startup restores each flag's Start Call Count. Reset Flag always clears to zero. Flag names are case-sensitive. Unknown names read false/count zero; Call Flag requires a declared flag. Invalid/duplicate definitions prevent startup.

Use Flag Action for calls and memory edits. The duplicate legacy action classes have been removed from production, rather than relying on picker visibility metadata. See Action catalog migration below for older external resources. The old compiled interpreter operations remain available to regression tests.

## Framework and game-mode actions

Add these objects directly to a mission event's Actions. Actions enter the existing bounded framework queue in authored order. They do not introduce a frame check. A change notifies relevant conditions; each event gets at most one eligible attempt per notification chain, including explicit Check now. Resetting cannot remove that loop guard.

| Action | Settings and behavior |
| --- | --- |
| **Flag Action** | Flag Name, Operation, Amount. **Call** checks the flag's conditions/repeatability and adds one only when accepted. **Set count**, **Add**, **Subtract**, **Reset to zero**, and **Restore starting count** directly edit its memory. Amount is used only by Set/Add/Subtract. Subtract stops at zero; overflow is rejected. Zero is false; any positive count is true. Count edits bypass activation conditions and notify when the count actually changes, even positive-to-positive. They do not reset events. |
| **Event Action** | Event Name and Operation. **Enable** permits future checks; it does not run the event. **Disable** cancels pending actions and preserves whether it ran. **Reset** clears run history and pending delay, retaining enabled/disabled state. **Check now** applies normal conditions, chance, once/repeatable and delay. It never forces actions through a failed condition. |
| **Message Action** | Delivery: **Players**, **Admins only**, or **Server log**. Title, Message, optional Factions and Warning. Factions apply only to Players; empty means everyone, including spectators. Admins only checks current admin login and ignores factions. Both popup deliveries support Warning, an optional 80-character title and a 512-character body without markup. Server log writes the body without a popup and ignores Title/Factions/Warning. See [Messages](Messages.md). |
| **Reset Random Chance Action** | Reset Name. Add the matching unique **Advanced > Reset Name** to a Random Chance Condition. The action clears its answer to false and restarts its full Check Interval. Roll Once may roll again. An unknown or duplicate name is rejected. Leave the condition's name empty when no reset is needed. Flags, event completion and random groups remain unchanged. A changed answer is combined through the normal enclosing logic; for example, None may become true when a roll is cleared. |
| **End Mission Action** | Result and Faction. Choose Mission complete, Faction victory, Faction defeat or Faction draw. Faction is required only for faction results. Uses Lobby2's existing authoritative Match-to-After-action transition; invalid factions and attempts outside Match are rejected. |
| **Respawn Tickets Action** | Faction, Set/Add/Subtract, Amount. Changes remaining tickets through Lobby2's existing ticket owner. Subtract stops at zero and overflow is rejected. No player is respawned by this action. Invalid factions, clients, inactive Lobby2 and After action are rejected. |
| **Respawn Wave Action** | Factions, Maximum Players, Use Tickets, Random Order. One wave of connected players with claimed KIA slots at their saved starting positions. Empty Factions includes all factions; zero Maximum Players has no cap. The cap is a total across factions and counts only successful respawns. Use Tickets costs one per success from the player's faction; disabling it neither requires nor spends tickets. Random Order shuffles eligible players without repeats; otherwise lobby slot order is used. |

Event names must be unique. Controls can enable, disable or reset a random-group child, but **Check now cannot run a child directly**: its group must select it normally. Disabling/resetting a group cancels its children's pending delays; it does not reset their run histories. Reset the needed children separately before checking the group. Resetting an event from true to false notifies references; a condition with Require False can therefore pass. Enable/Disable preserve that memory.

**Shared notification stack:** admin and mission notices never replace one another. Up to three fit on screen together; longer messages reduce the number that fit. Additional notices wait in arrival order, with at most 100 waiting. Each starts its own 15-second lifetime when displayed, fades in, fades out during the final second, and the remaining notices move into the gap. No per-frame framework polling is added; the native UI animation system handles animation. Extreme text overflow gets a shortened popup preview; the received message remains intact in owner-local history (the existing history retains its newest 100 messages). If the waiting queue is full, a new notice stays in history and a diagnostic is logged; visible messages are not evicted. Mode shutdown clears widgets, sounds, waiting messages, animation and expiry callbacks. Messages are not replayed to players who join later.

**Compatibility:** duplicate legacy authoring classes were removed in the action-catalog cleanup below. Current Lobby2 resources were checked after saving the open scene; none referenced those classes. External resources from earlier revisions require migration. The compiled legacy interpreter operations and explicit test fixtures remain intact. New missions use Random Chance Condition / Random Event Group plus Flag Action. New log output uses Message Action / Server log; the old Log Message class remains a hidden serialized compatibility reader.

**Verification for this slice:** production scripts and the optional regression fixtures are compiled through Workbench Reload. Source-level logic review covers ordering, count semantics, event rearming, legacy observers and reset scheduling. The new `GC_TestFrameworkActions()` fixture is explicit and has not been executed: the available runtime helper requires embedded Play, which the user excluded. Simultaneous-message rendering, animation, network delivery and actual game-ending/ticket effects likewise remain untested in-game.

## Referencing mission flags and mission events

**Mission Flag Condition** has Flag Name and **Require False** (unchecked matches true; checked matches false). It reads the flag's current result. The conditions held inside the flag control its activation; referencing it does not evaluate those conditions again or call the flag. This replaces the former Flag State name, with the same true/false behavior. Counts stay in Count Condition.

**Mission Event Condition** has Event Name and the same Require False checkbox. It reads whether the named event ran, without evaluating that event's conditions again or executing its actions:

- **Advanced > Repeatable (unchecked)**: starts false and remembers true after running.
- **Advanced > Repeatable (checked)**: becomes true for the resulting check cycle, then silently resets to false. A later run sends another notification.

Events are checked at initial setup and relevant state/flag/event notifications, not by continuously polling every condition. Event-only references do not recheck on unrelated phase changes; add Game Mode State when a phase change should also matter. A reference with Require False checked can pass at initial setup, before the named event has run. Referenced events still run through their normal framework conditions, chance and delay. Scheduling a delayed event does not mark it true; executing it does. If actions queue flag calls, those calls are processed in the same notification cycle.

Event names must be exact and unique across regular events, random groups and their children. Missing, duplicate or invalid event names fail closed, even with Require False checked or under a None group. An event with a disabled setting stays false until it runs (normally never). Empty conditions still mean true conditions, but do not make an event's execution result true before it runs.

A random group's result becomes true when it selects children. A selected child's result becomes true only if its own actions run; chance failure and pending delay do not count as a child run. A group with no eligible children does not publish a result.

These references nest inside Group Condition and Count Condition. Flags may also contain Mission Event Condition to activate in response to an event. A dependent event gets at most one eligible attempt per notification chain, avoiding repeatable feedback loops. Pulse resets do not send a second false notification. A delayed condition that requires a repeatable event to remain true will fail after that pulse resets; use a once-only source or a flag when the result must persist. Stopping clears pulses, and fresh startup clears all event results.

## Game Mode State

Choose **Mission preview**, **Role selection**, **Briefing**, **Match**, or **After action**. These are the existing authoritative Lobby2 phases; this feature does not introduce another phase system.

- **Require Other State unchecked** passes when that phase is active.
- **Require Other State checked** passes when a different valid phase is active.

The framework checks the initial state after the world loads, then listens through GC_LobbyManager's existing OnGameStateChanged callback. Duplicate notifications and ordinary ticks do not trigger another check. A late startup observes the current state; it does not invent a transition. The initial state can satisfy an Is not in condition too.

State conditions can appear anywhere inside Group Condition or Count Condition. A rule containing a state, flag or event-reference condition is checked at initial setup. Subsequently, event-reference rules check on referenced event notifications, and on phase changes only when they also contain Game Mode State. Flag-dependent rules check on referenced flag changes; existing flag-only rules also retain phase-change checks. Rules with Game Mode State check distinct phase changes. All top-level conditions must pass. A rule with no state, flag or event-reference conditions keeps the simple default: check once when Match begins.

Actions waiting on a delay are rechecked when due. A state change that makes their conditions false cancels the pending run. If the conditions remain true, the original wait continues. Explicit After action conditions and their delays may run after the match; shutdown cancels everything. Conditions with no valid state context fail closed, even inside None.

## The terms

| Setting | Meaning |
| --- | --- |
| Mission Event | A name, conditions, and actions to run when the conditions pass. |
| Condition | A requirement checked on relevant changes, and again after a wait. |
| Action | Something the mission event does. |
| Flag | Optional memory: true/false state and an accepted-call count. |

## Conditions: standard logic objects

Conditions are an **object list**. Use **+** to choose **Game Mode State**, **Mission Flag Condition**, **Mission Event Condition**, **Random Chance**, **Casualties**, **Total Deaths**, **Group Condition**, or **Count Condition**. Group and Count contain their own Conditions list, so they can be nested inside each other. A group has no number field; a count has a numeric threshold.

**Group Condition > Match** offers four choices:

| Match | Plain meaning |
| --- | --- |
| All are true (AND) | Every child must be true. |
| Any are true (OR) | One or more children must be true. |
| None are true (NOR) | Every child must be false. With one child, this reverses its answer. |
| Not all are true (NAND) | Passes unless every child is true. |

**None replaces Not.** Any group can contain other groups. XOR and XNOR have been removed.

Add one **Count Condition** object for number comparisons. Choose what to count, then set the comparison:

1. **Count Flag Calls**: unchecked counts true conditions; checked counts accepted mission flag calls.
2. **Comparison**: Greater than, Less than, Equal, or Not equal.
3. **Required Count**: the number to compare against.
4. **Flag Name**: used only for Mission flag calls. Enter the exact Mission Flag name, including when checking the flag containing this condition.
5. **Conditions**: used only for Conditions that are true. Each child can contain nested groups.

| Comparison with Required Count = 2 | Passes when |
| --- | --- |
| Greater than | 3 or more listed conditions are true. |
| Less than | 0 or 1 listed conditions are true. |
| Equal | Exactly 2 listed conditions are true. |
| Not equal | Any number except 2 listed conditions is true. |

For **at least two out of three**, use **Greater than**, set Required Count to **1**, and add three conditions. Each child contributes one true-or-false answer, regardless of how many conditions it contains. For exactly two, choose Equal and set the count to 2.

For example, **Greater than 1** can contain **All [A, B]**, **None [C]**, and **D**. It passes when two or three of those branches pass. The optional script tests exercise this nested layout. Another valid layout is **Any [All [A, B], All [C, D]]**.

With **Count Flag Calls** unchecked, these comparisons count satisfied conditions, not people or entities. With **Count Flag Calls** checked, they count accepted calls remembered by the named flag. Area Condition, Casualties and Entity Damage each provide their own true-or-false answer before the enclosing group counts them. Mission Event Condition can reference a named event's execution result. The fixed **True or false value** placeholder has been removed. Script-only test inputs live in the optional test addon and are not authored as mission conditions. Game Mode State is the first live check. Empty explicit groups still fail closed.

Every condition directly under a mission event must pass, so ordinary lists do not need an extra All group. **No conditions means always true:** an empty event Conditions list has no requirements to meet. Top-level events and random groups with no conditions run at match start; a child with no conditions is eligible whenever its group makes a selection. Chance, delay, disabled and run-once settings still apply. An explicit group needs at least one child. Empty groups, missing children and invalid required counts disable the event rather than becoming true under a negative operator. Nesting is limited to 16 levels and lists to 256 children.

Mission Flag Condition is one condition object with a Require False checkbox. Area Condition and Entity Damage are available; broad prefab-population searches and general timer conditions remain future slices. Casualties is available with its own faction filter. Scheduled Random Chance is now available as a condition; Advanced chance remains a separate action gate.

The shared serialized base is marked non-insertable using the engine convention. Some Workbench class pickers may still expose **GC_ConditionBase**, the required shared base that lets one list hold different condition types. Its title is **Do not use**, and its description explains what to choose instead. It does not perform a check. Replace base entries with a specific condition; incomplete base entries fail closed. The class name identifies it as a base; the display-title attribute says Do not use.

## Random chance and random groups

**Advanced > Random Chance** is a number from 0 to 1: **0.8 means 80%**, **1 means 100%**, and **0 means never**. The default is 1. Omitting the saved chance value uses its 100% Workbench attribute default. Chance controls whether actions run; it only spawns something if an action does so. The old separate Random object is removed.

Chance is checked once after conditions pass, before the wait. A failed chance waits for a new matching state change; it is not retried every tick. Conditions are checked again after the wait.

In **Mission Events**, choose either **GC_Event** or **GC_RandomEventGroup**:

- **GC_Event**: Name, Conditions and Actions, plus the Advanced category.
- **GC_RandomEventGroup**: Name, Conditions, Pick Count and Events, plus the Advanced category.

A random group chooses the requested number of different child events whose conditions currently pass. Disabled, waiting and completed once-only children are excluded. If fewer qualify, it picks those available. It never picks the same list entry twice in one selection. If none qualify, a once-only group stays available for a later matching state change.

The group's conditions decide when selection happens. Child conditions filter that selection; they do not trigger the group independently. Each selected child's own chance and delay still apply, so Pick Count is the number selected, not a guarantee that that many actions execute. A child that fails its chance is not replaced by another pick.

The group's delay happens before selection; child delays happen afterward. Waiting children must continue to satisfy both their own and their parent's conditions. A once-only group uses its one selection even if a selected child's chance fails or its later wait is canceled. Check the group's Repeatable setting for later matching state changes; children retain their own Repeatable settings.

Groups contain regular events, not other random groups. Conditions still nest normally. No weighted selection or full gameplay catalog is included. Random flag and Select random flags remain optional test actions; flags are not needed for event selection.

## Included examples

Use the current **Framework and game-mode actions** catalog above. Use Message Action / Server log for debugging. Old Call Flag, Reset Flag, Set Flag, Send Test Event, Random Flag and Select Random Flags are no longer production authoring types. Send Test Event has no authorable named-event condition to receive it. These actions expose no client request RPC or arbitrary script execution.

## Runtime and limits

Only the authoritative active Lobby2 game mode starts the framework, after world load, and supplies state observations. It uses the existing component callback with no additional global subscription. Shutdown and loss of active mode ownership stop pending work. Explicit After action conditions can run in POSTGAME; old non-state delayed/timer work is canceled there. Mission events/flags do not replicate as a general mission-state API. Flags reset on a fresh engine start; stopped values remain available for diagnostics. The total event limit is 256, including random groups and their children. Each random group holds up to 255 regular events, with Pick Count from 1 to its configured child count. Initial flags, actions and condition lists are limited to 256; condition nesting is limited to 16. A bounded event queue stops runaway chains. Malformed mission events fail closed and report an error.

## Updating earlier prototypes

This cleanup changes the saved authoring schema. It is **not** a drop-in deserializer for unchanged older resources. The supplied project example and the user's open example layer were migrated. External resources using GC_SimpleMissionRule, GC_RuleDefinition, old GC_MissionRule fields, specialized event classes, or old condition classes need conversion/recreation before use. The preceding GC_MissionCondition + m_iType format also changed: recreate groups using Group Condition and its Match setting. Old separate true/false flag checks can be recreated using Mission Flag Condition and its Require False checkbox. Count comparisons now count satisfied child conditions. The known project example and open Arland condition were migrated. Keep a backup. Do not combine removed prototype scripts with this version.

The old interpreter behavior remains covered by regression tests. A historical fixture is loaded through an isolated test-only adapter; this proves its behavior, not unchanged production resource compatibility. The original MissionFramework_v0_1.zip was unavailable, so exact archive compatibility is not claimed.

## Verification and provenance

Workbench 1.9.0.37: 560 checks passed before the final action-order correction (48 flag memory, three serialized flag defaults, 30 event-group/chance, 85 state conditions, 189 standard logic, 148 interpreter, 22 authoring, six no-flags resource, four state-example resource, five historical-fixture and 20 randomness checks). Real Lobby2 transitions were also tested through Preview -> Role selection -> Briefing -> Match -> After action: each action fired once, duplicate notifications were ignored, unauthenticated phase changes were rejected, and stopping prevented further execution. The production manager CallFlag path also passed count/pulse and disabled/shutdown/stopped rejection checks. The temporary test configuration was discarded by stopping Play. Dedicated/remote-client behavior was not newly tested. Known unrelated Arland GUID, missing menu-preset and Workbench tool-leak diagnostics remain.

Installed engine source: game/data/data007.pak/scripts/Game/GameMode/SCR_BaseGameMode.c (SHA256 c02bf603991e03e956d880ffaf643cfbde46f87a7d31f2fb1fc0918e95c16652), lines 434-465, dispatches OnGameStateChanged to components on server and clients. The manager gates mission execution to the active authoritative Lobby2 owner.

TilW was studied as inspiration, not copied. Reference commit: 4a5300f94fb9323ce4942ef296bcffcdda922626. Its random-flag and flag-sampling initialization/actions informed the use cases. This framework supports random selection of regular events without flags.

- https://github.com/Til-Weimann/tilw-mission-framework/blob/4a5300f94fb9323ce4942ef296bcffcdda922626/scripts/game/TILW_Instructions.c
- https://github.com/Til-Weimann/tilw-mission-framework/blob/4a5300f94fb9323ce4942ef296bcffcdda922626/scripts/game/TILW_MissionFrameworkEntity.c

The editor label Mission Rules is now Mission Events. The project's saved list and enable fields were migrated to m_aMissionEvents and m_bMissionEventsEnabled; external resources using the old names need the same rename. The regular authoring class is now GC_Event; internal interpreter names retain MissionRule.

### Migrating saved conditions to this version

The editor now uses two composite types:

- **GC_ConditionGroup**: m_iMatch (0 All, 1 Any, 2 None, 3 Not all) and m_aConditions. No number field.
- **GC_ConditionCount**: m_iCountFrom (0 true conditions, 1 mission flag calls), m_iComparison (0 Greater than, 1 Less than, 2 Equal, 3 Not equal), m_iRequiredCount, m_sFlagName and m_aConditions.

Replace the former All/Any/None/NotAll classes with Group Condition and the matching mode, keeping their children. An old GC_NotCondition becomes a None group with its former m_Condition as the sole child.

Replace separate comparison classes with Count Condition. For integer counts, >= N can become > N-1 when N is positive; <= N can become < N+1 when the new threshold remains within 0-256. Boundary cases require a deliberate equivalent group; do not enter an invalid threshold. Not equal is now a direct Count Condition choice; the older None group containing an Equal count remains equivalent. Old fixed m_fFirstNumber/m_fSecondNumber comparisons need deliberate rebuilding; they do not represent live checks automatically.

The shipped no-flags example now uses a Match state condition and a fixed wait, using a once-only random group without Boolean placeholders. The user's empty Greater than object was renamed without changing its meaning. External resources need the same conversions before use. Removed XOR/XNOR classes require deliberate replacement; internal values 8 and 9 are rejected rather than reused. Internal legacy count modes remain for historical interpreter behavior, not as editor choices.

The former separate Flag true/false classes remain removed. Recreate them with the single Mission Flag Condition condition and the matching choice.

The GC_BooleanCondition authoring class has been removed. External resources using it need deliberate removal or replacement with appropriate real checks when available; do not delete a false restriction without considering how that changes the event. No saved world in the inspected project referenced this class at removal.

### Migrating the former Event selector

Remove the obsolete GC_MissionEvent.c file when installing a source snapshot. GC_MissionRule.m_Event and the GC_MissionEvent authoring type are removed. Match started becomes a Game Mode State condition: m_eGameModeState = 1 (GAME), m_iMatch = 0 (Is in). Invert uses m_iMatch = 1. The saved empty Match-start selector in the inspected Arland scene was converted.

A once-only Timer after match start can use Match / Is in with Advanced > Delay; this is how the no-flags example is migrated. Repeating timers and named-event receivers need deliberate migration when those conditions are added; they are not silently converted to unconditional events. The old two-event flag-chain sample is deliberately replaced with independent Match and After action examples. The compiled interpreter still supports its historical triggers for regression/extension callers, but they are no longer editor options.

### Migrating regular events and randomness

Rename saved GC_MissionRule objects to **GC_Event** and remove obsolete GC_MissionRule.c when installing a source snapshot. The Mission Events list now accepts GC_Event and GC_RandomEventGroup through its shared serialized base GC_EventDefinition. Incomplete base entries fail closed; choose a concrete event type if a picker exposes the base.

Move old Random > Chance Percent into Advanced > Random Chance, dividing by 100. Remove m_Random from authored events. Convert One random action into a random group with Pick Count 1 and one regular child event per former action. Old additional random delays require deliberate redesign; they are not silently preserved. The shipped no-flags example deliberately changes its former fixed-plus-random delay to a fixed two seconds. Historical random behavior remains available only to compiled interpreter callers and regression tests.

This slice's live checks include saved example deserialization, default chance when omitted, chance boundaries, filtering, distinct picks, insufficient eligible children, delays, cancellation and repeat behavior. Full native object-class picker options and dedicated/remote-client behavior were not newly verified.

Script callers constructing authoring containers directly must set their fields explicitly (including Random Chance = 1). Attribute defaults apply when Workbench loads serialized containers. Compiled runtime rules default to full chance. Field initializers are intentionally avoided on attributed fields because they can overwrite serialized settings.

### Flag-memory migration and example

Existing declared Mission Flags default to non-repeatable. Repeatable flags now keep true and their successful-call count until reset; they no longer produce temporary pulses. Set False resets the count and rearms the flag. To initialize a flag true, set Start Call Count to 1 or more. Initialization records memory without making a call or checking activation conditions. Initial framework condition checks can observe it. Remove old m_bStartsTrue (or m_iStartingValue) fields from external resources. No such saved fields were found in current project resources. This intentionally changes earlier prototype behavior.

Server extensions can call GC_LobbyManager.CallFlag(name) and read GetFlagCallCount(name). Calls require the active authoritative Lobby2 owner, an enabled framework and a running engine. This adds no client RPC. True from CallFlag means queued; a call queued by another action is checked again when processed. Counts are server-local, not a replicated general-purpose state API. The existing bounded queue still stops oversized event chains.

Final verification scope: at the user's request, the action-order correction was checked by script compilation and source-level logic review, without another in-game run. Three added ordering regressions compiled but were not executed. The earlier 560 passing checks and manager checks predate that correction.

The Advanced field formerly named Wait Seconds is now Delay, still measured in seconds. Current events store m_fDelay directly. Older GC_RuleOptions objects require flattening into the owning event, with m_fWaitSeconds renamed to m_fDelay where applicable. Internal compiled/legacy fixtures retain their original field name.

Count Condition now offers Not equal (saved comparison value 3), implemented as the inverse of a validated Equal check. Existing values 0/1/2 retain their meaning. Empty or malformed condition lists still fail closed. Boundary and nested-group test cases were added; this update was verified by source-level logic review and production script compilation, without an in-game test run.

### Combined Count Condition

The separate GC_FlagCallCountCondition authoring class is removed. Convert saved entries to GC_ConditionCount with m_iCountFrom = 1, keep m_sFlagName and m_iComparison, and rename m_iCount to m_iRequiredCount. Preserve the former omitted defaults explicitly: comparison 1 (Less than), required count 5. The shipped FlagMemoryExample is migrated. Existing child-condition counts keep m_iCountFrom = 0 by default. Both sources support all four comparisons. Counts of true conditions retain their 0-256 threshold limit; flag-call thresholds allow 0-1000000. Settings for the unselected source are ignored. This consolidation received script compilation and source review only, as requested.

### Named-reference migration

Rename saved GC_FlagStateCondition entries to GC_MissionFlagCondition; m_sFlagName and m_iMatch retain their values (0 true, 1 false). The project fixture and source callers are migrated. The new GC_MissionEventCondition uses m_sEventName and m_iMatch. This slice received production script compilation and source review only; no in-game tests or project restarts were performed.

Design boundary: each mission event and flag owns its conditions, repeat behavior, delay and activation rules. Outside references read published results only. They never invoke the referenced object or re-evaluate its internal conditions. Duplicate-name detection includes authored entries that fail to compile, so incomplete duplicates cannot become ambiguous valid references.

The former GC_MissionCondition shared base is renamed GC_ConditionBase, in GC_ConditionBase.c. Remove the old GC_MissionCondition.c when installing a source snapshot. Update script references and any saved base entries to the new name; the current project was migrated. Base entries still do not perform a check and must be replaced with a specific condition before use.

## Scheduled Random Chance condition

Add **Random Chance** inside an event or flag's Conditions list, including inside Group Condition and Count Condition.

- **Check Interval**: seconds between rolls; default 60, allowed 0.1 to 86400. The first roll waits one full interval after Match starts (or after the framework starts if Match is already active). No rolls run in preview, role selection, briefing or after action.
- **Chance**: a 0-to-1 float, default 1. `1` means 100%, `0.05` means 5%, `0.0005` means 0.05%, and `0` never produces true. Decimal entry is not limited to a one-percent spinner step.
- **Roll Once**: unchecked (default) makes a new roll each interval. Checked makes exactly one roll after the first interval and holds that first result for this Match, including a failed roll. It does not keep trying until success. Event Repeatable controls action execution independently.
- A result is held until the next scheduled roll (or until Match ends when Roll Once is checked). Reading a condition never rerolls it. Other matching flag/event changes can use that held result, so a repeatable event can also run between rolls when those other conditions change.
- Before its first roll, each random condition is false. Groups apply ordinary logic: Any may pass through another true child, and None may pass while the random answer is false. There is no hidden random requirement overriding an enclosing group. Different intervals keep their own schedules and combine their latest results. Each due roll notifies its owning event/flag even if its answer has not changed, allowing repeated successes. Multiple leaves due in the same update are rolled before owners are checked, with one eligible event attempt per notification chain.
- Events still run once by default. Choose **Advanced > Repeatable (checked)** for repeated activations. Flags retain their own Repeatable and accepted-call-count rules. No flag is required to use this condition.
- All/Any/None/Not all and Count Condition combine this answer normally. A failed roll can therefore satisfy None. Invalid chance/interval values never become passing answers through inversion.
- Delay keeps its existing meaning: latest conditions must pass when the delay ends. A later failed roll can prevent the delayed event from running. Pending events do not stack additional delayed runs. Advanced > Random Chance is a separate additional chance gate; leave it at 1 when only this condition should control probability.
- A random condition on a Random Event Group schedules group selection. A random condition inside a child only filters that child; it does not start its parent group. All random-group eligibility rules still apply.
- Leaving Match resets random answers to false and stops their rolls; pending actions are rechecked with normal group logic and existing phase-lifetime rules. Stop/fresh Start clears them too. Late updates roll once and schedule the next interval from that update, without replaying missed rolls in a burst. The existing manager tick supplies scheduling, with its authority/mode guard, and now also supports flag-only configurations.

For a 5% chance every 90 seconds, set Check Interval to `90` and Chance to `0.05`. Check Repeatable on the owning event if it should continue after a successful execution. This is a fresh independent chance each interval, not a guarantee that one in every twenty intervals succeeds.

This slice adds no weighted selection, cooldown, changing probability or shared random-result catalog. Existing Random Event Groups handle selection; optional flags can remember a result when the mission explicitly needs shared memory.

Verification: production Workbench script compilation and source-level logic review. Deterministic regression scenarios are provided in the optional `GC_RandomConditionTests.c`; they were not executed in-game, in accordance with the requested verification scope.

## Casualties condition

Add **Casualties** directly to an event's Conditions, inside a group/count, or to a Mission Flag's Conditions. All casualty settings stay on this one object; there is no separate faction condition or mandatory flag.

- **Factions**: an array of exact faction keys, such as US and USSR. An empty array includes every faction, including unaffiliated eligible characters. Selected factions are combined into one starting population. Duplicate keys do not double-count; blank entries are invalid.
- **Count**: Players (default), AI, or Players and AI. Spectator bodies, characters already dead at capture, and unoccupied playable slots are excluded. Connected players' living claimed slot characters count as players even when queued possession is still catching up at Match entry. Player-controlled characters outside slots are also included. AI is discovered through the native AI-world agent registry, excluding playable groups and player/slot characters.
- **Casualty Ratio**: a float from 0 to 1, default 1. `0.7` means at least 70% of the selected starting lives have died. `1` requires everyone; `0` requires no deaths but still requires a nonempty starting group. With ten starting lives, 0.7 passes on the seventh death. With three, 0.7 requires all three.

The baseline is captured once at the authoritative manager's Match-entry callback, before its mission events execute. A framework first started during Match captures the living group at that time; it cannot reconstruct an earlier baseline. The starting faction and Players/AI classification stay fixed for each life. Respawns, reinforcements, later joins, faction switches and possession changes do not expand or reclassify that baseline. Death is remembered once; a replacement life does not undo the original death. Disconnecting, unconsciousness, and deletion without a death notification do not count as casualties. An empty matching population returns false. Ordinary All/Any/None/Not all/Count logic still applies to this answer; invalid authored settings fail closed.

The existing authoritative `OnControllableDestroyed` callback records confirmed player and AI deaths, then checks casualty-dependent top-level events and flags. Player slot/death handling finishes before mission actions run. Random-group children remain filters and do not trigger their parent. No per-frame character scan, additional death listeners, client RPC, or shared global override is added. Event and flag Repeatable settings retain their existing meanings; a repeatable owner can react to later casualty notifications while its full conditions pass. The final snapshot remains readable after Match, with no new deaths recorded; shutdown clears the tracker and engine context.

Installed engine evidence: `game/data/data007.pak/scripts/Game/GameMode/SCR_BaseGameMode.c`, SHA256 `c02bf603991e03e956d880ffaf643cfbde46f87a7d31f2fb1fc0918e95c16652`, lines 1347-1366 dispatches controllable destruction to game-mode components. Existing Lobby2 native player/AI discovery and slot lifecycle paths are reused. Production source was checked by Workbench script compilation and independent source review. Optional deterministic snapshot/interpreter scenarios in `GC_CasualtyConditionTests.c` were authored but not executed; no in-game tests, per the requested scope.

## Total Deaths condition (respawn missions)

**Total Deaths** counts confirmed character deaths during Match, including deaths of respawned characters, later joins and reinforcements. It does not use a starting population or a casualty-ratio field.

- **Factions**: exact faction keys; empty means all. Uses faction at death, unlike Casualties which keeps each starting life's faction.
- **Count**: Players, AI, or Players and AI. Player victims reported by the engine, claimed player-slot characters, and tracked starting player lives count as Players. Spectators and unoccupied playable AI slots are excluded. The same faction/population matching implementation serves both condition types.
- **Comparison**: Greater than, Less than, Equal, or Not equal.
- **Required Deaths**: a whole number. Greater than 4 means five or more deaths; default Greater than 0 means the first death.

Each character life adds at most one death. Respawning creates another life whose later death counts again. A later life does not change Casualties' starting population or counted starting deaths. For example, one starting player can reach 100% Casualties after the original life dies, then accumulate five Total Deaths over five lives. Deleting a living body or disconnecting adds no death. Total Deaths starts at zero even when the initial population was empty. Equal 0 and Less than a positive threshold can therefore pass at Match start. Final totals remain readable after Match; Stop/fresh Start clears the engine context.

Two objects keep the different fields clear: **Casualties** has a ratio; **Total Deaths** has a comparison and number. Both integrate into the same nested condition lists and optional mission flags, using the same authoritative death notification and tracker. There is no separate faction condition and no irrelevant ratio field on Total Deaths.

### Spectator exclusion audit

Casualties and Total Deaths reject every entity carrying `GC_LobbySpectatorBodyComponent` before faction/population selection. This applies both to the starting-life capture and the death-recording path; empty faction filters and Players and AI do not bypass it. Lobby2's dedicated spectator prefab contains that component, and the manager uses that prefab or reuses the same marked body for spectator/reconnect assignment. Following another player moves the spectator camera/body rather than adding another combat life.

The actual soldier's death remains a casualty when its player subsequently becomes a spectator. Checking the controller's current spectator state instead of the victim body could wrongly suppress that legitimate death. Connected claimed slot characters awaiting Match-entry possession remain intended player lives, not spectator bodies. AI is counted only by the AI or Players and AI selection. Disconnect/delete notifications do not increment either death measure.

This conclusion is based on source/prefab inspection, including an independent audit; it is not a new live spectator/respawn test. Custom spectator prefabs must retain `GC_LobbySpectatorBodyComponent`; unrelated mods' unmarked spectator characters cannot be identified reliably by this marker-based contract. The built-in Lobby2 paths inspected retain it. Only tooltip/documentation clarification was needed; death-counting behavior is unchanged.

### Entity Damage

Add **Entity Damage** to any event or flag's Conditions list, including inside Group Condition or Count Condition. Name the target entity in the World Editor, then enter that exact, unique **Entity Name**. This refers to one placed entity, not every instance of a prefab and not a player's account name.

- **Destroyed**: the engine reports that the main damage area is destroyed. For a character this is its main health area, not unconsciousness.
- **Not destroyed**: that damage area is still intact enough not to be destroyed. It can be damaged.
- **Damage amount**: damage has reached **Damage Needed**, from 0 to 1. For example, 0.5 means at least 50% damage. Repairs below the threshold make the condition false again. Zero includes an undamaged supported target; one requires all health to be gone. Use Destroyed for the engine's destruction state rather than assuming every prefab maps destruction to the same health threshold.
- **Advanced / Damage Area**: optional exact hit-zone name. Leave blank for the default area. A named area belongs to the target's own damage manager; child objects such as separately attached wheels need their own entity name. No child hierarchy search is performed.

The native inspector displays Damage Needed alongside Check; its description explicitly limits it to Damage amount. It is ignored for Destroyed and Not destroyed. No additional flag, prefab component, player/vehicle/building selector, health modification, or global class override is required. The underlying entity must already have an `SCR_DamageManagerComponent` and an `SCR_HitZone` for the selected area. Decorative or indestructible scenery without that system is unsupported, rather than presumed destroyed.

Example: name a truck `SupplyTruck`, add Entity Damage with that name and Destroyed, and add the desired actions. For a combined objective, put that condition and a Mission Flag Condition inside All. A damage notification never bypasses the other conditions. A condition on a random-group child filters that child; it does not trigger the parent group's selection. Repeatable and Delay remain owned by the event. A repeatable event can run again when a damage condition changes answer and the complete condition tree passes, for example after repair and a later threshold recrossing. Additional hits while the same answers remain true do not retrigger it.

Runtime ownership: GC_LobbyManager passes its world only to the authoritative, active Lobby2 mission engine. Compiled damage leaves share one runtime observation per resolved native hit zone, using the same manager/hit-zone API for characters, vehicles and destructible buildings. Initial resolution occurs at framework startup; unresolved targets are retried at Match entry. Targets must exist by that point; later dynamically spawned replacements are not automatically followed. Once resolved, the observation tracks that specific entity for the mission. Names must be unique (the native name lookup chooses the named entity; this is not a duplicate-name search).

The observation reads initial health/state and subscribes to the native health and damage-state invokers. A native callback immediately refreshes the shared observation and notifies only its registered condition leaves. Their cached answers are updated together before mission events run. Notifications are sent when a leaf's answer or availability changes: duplicate health/state callbacks and additional damage above an already-passing threshold do not repeatedly fire the same check. Repairs can make an answer false, and crossing the threshold again can trigger a repeatable event. Destroyed / Not destroyed still use the native damage state, not a guessed health threshold.

Entity Damage is not polled in the shared 100 ms Advance path and uses no CallLater, frame handler or delayed damage dispatch. Startup and Match entry still resolve targets, share subscriptions by native hit-zone identity (including default/named aliases), and evaluate initial answers once. During Match, callbacks drain through the existing bounded framework event queue immediately. If an event action causes further damage while that queue is already running, the new notification joins the same queue rather than recursively executing another interpreter. Distinct native state transitions can still produce distinct notifications; this does not invent a universal one-notification-per-bullet contract.

There is no damage or deletion polling and no world scan. A deleted intact target becomes unavailable when its condition is next evaluated, but deletion without a damage callback does not itself schedule a damage event. A confirmed destroyed result survives wreck cleanup; if repaired before removal, the updated result applies. Missing/unsupported/lost intact targets fail the containing condition tree before inversion, so None cannot turn an unresolved reference into success. Stop removes both native subscriptions and the framework callback, clearing observations and answer caches. No client listeners, global overrides or new RPCs are added.

Research: TilW's `scripts/Game/Components/TILW_EntityDamageFlag.c` uses a damage manager, optional named hit zone and damage-state subscription to write a flag, with continuous/one-shot listening and optional initial-health mutation. GC reuses the engine pattern rather than that flag component: observation stays in the condition runtime, memory stays in existing events/flags, and this condition never changes health. Installed engine evidence (catalogue revision `95a1918a7fed40bf884083293ae050d6`): `scripts/Game/HitZone/SCR_HitZone.c` lines 18–64 supplies both invokers; `Prefabs/Characters/Core/DamageManager_Character_Base.ct` lines 1–13 selects the default Health area; character manager ancestry passes through `ExtendedDamageManagerComponent: SCR_DamageManagerComponent`; `SCR_VehicleDamageManagerComponent` and `SCR_DestructibleBuildingComponent` inherit the same manager. The CowShed long base prefab declares an `SCR_HitZone Default` on its building component. These are source/declaration/resource findings, not live damage tests.

Validation: production scripts compiled successfully through Workbench Reload, with zero SCRIPT errors. Logic and lifecycle are source-reviewed. Optional GC_EntityDamageEventTests.c covers immediate dispatch without Advance, shared leaves, duplicate callbacks, threshold recrossing, reentrant damage and Stop cleanup; it was compiled but not executed. In-game damage, repair, deletion and multiplayer tests were deliberately not run at the user's request. Existing unrelated Arland resource/menu diagnostics remain outside this change.

### Area Condition: radius or drawn polygon

Use **Area Condition** for named objects or living Players / AI inside/outside a shape. Use **Living Characters** for a living-population count anywhere in the mission. Area Condition has **Targets**, **Area**, **Inside / Outside**, and optional **Requirement** (Count or living-character Proportion). Both are ordinary framework conditions, with shared population sampling and no condition logic on the shape entity. Area is the exact, unique world name of the area entity. Geometry belongs to the referenced area; there is no shape selector or unused radius field in the condition. Flags remain optional, and actions/repetition/delay remain on the mission event. The future entity-picker UI can replace manual name selection without moving these responsibilities.

Place either prefab from `Prefabs/Logic/Mission`:

- **RadiusArea.et**: name it and set Radius in metres (default 10). It defines a 3D sphere including height. A point exactly at the radius is inside. An editor-only wire sphere previews the range. Scaling the entity does not change the configured metre radius. A radius area can be parented to a moving entity to follow it.
- **PolygonArea.et**: name it and draw a closed outline with 3–128 points using the normal Workbench Vector tool. The existing Map / AO Polygon tool also accepts this area and can generate a regular polygon before you edit it. Its prefab deliberately has no inherited points. Like the AO check, the polygon uses the horizontal XZ outline and ignores height; its point heights are not floor/ceiling limits. Use a simple outline without intersecting edges. Generic closed `PolylineShapeEntity` objects are also accepted. An existing AO polygon contributes its authored shape only, not its restrictions or inversion settings.

Example: place a Radius Area named `PickupZone`, set Radius to 10, and name the truck `SupplyTruck`. Add Area Condition: Targets = Named Entities, Names = [`SupplyTruck`], Area = `PickupZone`, Check = Inside. To use a drawn compound instead, point Area at a named Polygon Area; the rest of the condition and event are unchanged. Named Entities accepts a list. For living characters in an area, choose Targets = Players / AI with optional faction filtering. Count compares matching targets; Proportion compares eligible living characters on one side with all eligible living characters. Automatic arbitrary prefab discovery is not included. It tests the entity's origin, not overlap of its bounding box, collision mesh or vehicle hull. Dead named entities are still positions; Players / AI excludes dead characters. Combine Entity Damage when a named object being intact matters.

**Scheduling and notifications.** Startup and phase changes take a snapshot; during Match the existing server-local interpreter clock schedules one area sample per second. This adds no runtime frame/fixed-frame callback, per-target timer or per-target call queue. The interpreter's existing 100 ms tick only checks the next sample deadline; it does not perform area geometry checks every tick. Late updates take one sample and schedule the next one a second later, without replaying missed samples. The editor-only radius preview does redraw while editing; it does not run gameplay checks. Movement is observed periodically, and result changes produce targeted framework notifications. Initial Match state is also notified. Stable answers produce no new area notifications. A repeatable event therefore reacts to later matching changes, rather than firing every second while a target stays inside. Another condition's notification may still activate an event while the cached area condition passes. Full nested conditions always apply. Child conditions filter random-group children without triggering the parent selection. Delayed actions recheck the full tree using its latest sampled positions. No new RPC, client-side tracking or mandatory flag is added.

**Caching and cost.** Each unique name has one weak entity reference and one position read per sample. Names are resolved only while their reference is missing; missing/deleted targets can be found again on a later scheduled sample, including named replacement entities. Each area shares its geometry cache across targets, and identical target-selection/area combinations share inside and total counts, independently of comparison or side. Each entity/area membership calculation is also cached within the sample across target selections. Radius uses `vector.DistanceSq` against radius squared. Polygons cache authored points and their world-space bounding box; points are transformed again only when the shape's world transform changes or a replacement shape is resolved. The bounding box rejects distant targets before `Math2D.IsPointInPolygonXZ`, the engine function already used by GC's AO. There is no raycast, physics overlap, `QueryEntitiesBySphere`, broad world scan or unconditional all-characters-times-all-triggers traversal. Population selections examine the native character snapshot, sharing geometry membership results across filters. Work is capped at 256 target-selection/area combinations and 128 points per polygon. Excess combinations fail closed and produce a diagnostic. This is a code-level cost bound, not a measured runtime performance claim.

**Failure and lifetime rules.** Missing named target/area, unsupported area type, open/degenerate/oversized polygon, or invalid radius make the containing condition tree unavailable before inversion: Outside/None cannot turn missing data into a successful event. Diagnostics report missing names and invalid shapes once, avoiding repeated log spam. Cached references are weak; deleting a target never means it successfully left the area. The next sample can resolve a replacement with the same name. Names must be unique; native name lookup is not a duplicate-name search. Stop drops the tracker and all caches; only the current authoritative Lobby2 owner advances it. There are no global class overrides or registration hooks to clean up. Runtime editing of polygon vertices/closed state, including separate AO `SetPolygon` overrides, is outside this slice; save geometry and reload/restart to capture it. Moving or rotating the authored shape is sampled normally.

One-second sampling deliberately trades fine movement detail for lower work. Entry/exit can be detected roughly one interval later (longer under a stalled server), and a complete crossing between samples can be missed. This is intended for mission presence checks, not projectiles or precise crossing detection. Boundary behavior for polygons follows the engine polygon function; no competing geometry algorithm is introduced.

**TilW comparison and evidence.** The reviewed TilW `TILW_TriggerSystem` uses a server-owned character list, defaults to 50 characters per processing frame and 1000 ms idle time after completed counting rounds, then evaluates triggers. Each registered character is offered to every trigger. Its Presence Trigger separately resolves named entities on each query and directly checks radius; its faction/extraction paths use the shared radius/polygon helper. The base trigger publishes result changes, optionally sets a flag, and also owns capture/status/repetition settings. GC keeps the low-rate sampling and result-change approach, but checks configured target selections, caches lookups/geometry, shares identical checks and leaves flag/action/repeat behavior in the existing framework. Source: https://github.com/Til-Weimann/tilw-mission-framework/blob/main/scripts/Game/Systems/TILW_TriggerSystem.c and `scripts/game/Entities/Triggers` at reviewed checkout `4a5300f94fb9323ce4942ef296bcffcdda922626`. Installed API evidence: `game/core/data.pak/scripts/Core/generated/Types/vector.c` declares `DistanceSq`; `IEntity.c` declares world transforms and local-to-world coordinate conversion; `ShapeEntity.c` declares `IsClosed`. GC_AOLimitEntity already uses `GetPointsPositions`, `CoordToParent` and `Math2D.IsPointInPolygonXZ`.

Validation: Workbench Reload compiled the production scripts and editor polygon-tool changes with zero SCRIPT errors. Logic, cache lifetime, scheduling, nesting and performance structure received source review. No in-game movement, preview appearance, multiplayer or benchmark test was run, per the user's requested verification scope. Existing unrelated Arland resource/menu errors remain.

### Area target selections prototype

Choose **Targets > Named Entities** for exact world names, or **Targets > Players / AI** for eligible living characters. Both options are available directly on Area Condition. Named Entities accepts 1-256 names; duplicates count once and every listed name must resolve. Players / AI has Who = Players, AI, or Players and AI, plus a Factions array. Empty Factions means all factions; selected factions are combined and duplicate keys are ignored. Uses the character's current faction. Faction settings do not appear on Named Entities.

Players means living characters currently controlled by connected players. Spectator bodies are rejected by `GC_LobbySpectatorBodyComponent` before counting. Dead characters are excluded; unconscious but living characters still count. AI comes from `AIWorld.GetAIAgents`, excludes player-controlled characters, all Lobby2 playable-slot bodies and agents in playable groups, and is deduplicated by entity. A character waiting for initial possession is not counted as a player until possession completes. Disconnected/unoccupied playable bodies are not reclassified as mission AI. This uses current membership, not the casualty condition's frozen starting-life cohort.

The optional **Requirement > Count** compares targets on the selected side using Greater than / Less than / Equal / Not equal. Unset means Greater than 0 (at least one). Examples: Players + US + Inside + Greater than 4 requires five living US players inside; AI + USSR + Inside + Equal 0 requires no eligible USSR AI inside. Outside counts targets outside; it does not mean every target is outside. A valid empty population has count zero, so Equal 0 can pass. Missing named targets or unavailable population sources fail the whole containing condition tree, including under None. Count Condition still counts true child conditions; Area Count counts matching targets.

One native population snapshot is acquired per scheduled sample only when needed. PlayerManager owns player discovery; AIWorld.GetAIAgents is consulted only for AI/Both selections. New controlled characters, registered AI, respawns and faction changes are reflected on the next sample. There is no character-registration override, spawn modification, spatial query or fallback discovery. The snapshot records faction and position once per included character. Above 4096 registered AI agents or included characters the population check becomes unavailable, rather than reporting partial counts; a diagnostic logs once. If AI is requested but its native world is unavailable, population checks fail closed. The explicit owning manager must still be the active authoritative Lobby2 manager. Stop releases the snapshot and caches.

The shared counts update each sample, but each condition notifies the framework only when its final comparison answer or availability changes (plus the initial/phase observation). Changing from one inside target to two does not repeatedly fire an event whose Greater than 0 condition stayed true. Different count comparisons sharing one observation have independent result-change tracking.

TilW's Presence Trigger resolves named objects; its Faction Trigger filters dead characters, spectators and unplayed slots and offers a player-only switch plus faction ratios. This prototype keeps one geometry/scheduling system and uses native player/AI ownership instead of adding a global character override. Capture delays/messages and arbitrary type/prefab discovery are not implemented; extraction proportions are provided by Area Condition below.

**Type/prefab discovery is deferred at the user's request.** Checking a known entity's prefab ancestry is separate from discovering all instances. Searches of installed public sources found group-local vehicle lists but no complete public list of all spawned vehicles; the inspected Vehicle/BaseVehicle declarations do not provide one. No world scans, global prefab searches, vehicle lifecycle hooks or fallback discovery were added. Any future Lobby2-only vehicle registration callback requires separate agreement and initial-load/shutdown verification; it is outside this prototype. The delivered scope is Named Entities and Players / AI with optional faction filtering, using the shared area checks.

**Schema migration:** the first prototype's single `m_sTarget` string is replaced by `m_Targets GC_NamedAreaTargets` with `m_aNames { "old-name" }`. Keep `m_sArea`; rename `m_iCheck` to `m_bOutside` (0 = inside, 1 = outside); leave Count unset to preserve a single named target's Inside/Outside behavior. Inspected project resources contained only an empty Area Condition, so no configured saved target needed conversion. External snapshots using the old field must be converted. User-authored scene changes were not replaced or included in this source update.

Validation: production script reload/compilation and independent source review cover target construction, comparisons, deduplication, population ownership, spectator exclusion, cache lifetime and change notifications. Runtime membership, respawns, faction changes and editor-picker behavior were not tested in-game at the user's request.

### General living counts and area extraction

**Living Characters** is a general population condition. It has **Who** (Players, AI, or both), optional **Factions**, and optional **Count**. It counts eligible living characters anywhere in the mission. There is no Area or Proportion option on this condition. New checks with Count unset use Greater than 0. Count supports Greater than, Less than, Equal and Not equal.

**Area Condition** owns all area-specific settings. Choose **Targets > Players / AI**, set Who and Factions there, reference the named Radius/Polygon Area, leave Outside unchecked for inside, or check it for outside, then choose **Requirement > Count** or **Proportion**. Leave Requirement unset to require at least one matching target (unless an older saved Count is present; see compatibility below). Named Entities supports Count only; a Proportion with Named Entities is rejected.

For extraction, use Area Condition: Targets = Players / AI, Who = Players, Factions = US, Area = ExtractionZone, Outside unchecked, Requirement = Proportion / 0.9. This passes when at least 90% of all currently living US players are inside. The denominator includes eligible living players outside too. Nine of ten passes; eight of nine fails; nine of nine passes. Deaths, respawns, disconnects and faction changes update the population on the next scheduled sample. Use Casualties to compare losses with the starting population instead.

Proportion 1 means everyone. An empty eligible population makes Proportion false, even at threshold 0. A nonempty population at threshold 0 passes regardless of location. A valid empty population has Count 0, so Count Equal 0 can pass. Outside uses the number outside and the same total. Missing/invalid areas or unavailable population data remain unavailable, including under None. Living Characters includes unconscious living characters and excludes spectators, dead characters and unoccupied playable slots, using the same existing filters as area population checks.

| Condition | Settings | Meaning |
| --- | --- | --- |
| Living Characters | Players; US; Count Less than 5 | Fewer than five living US players remain anywhere |
| Area Condition | Players / AI: Players, US; ExtractionZone / Inside; Proportion 0.9 | At least 90% of living US players are inside |
| Area Condition | Players / AI: AI, all factions; BaseArea / Inside; Count Equal 0 | No eligible living AI are inside |
| Area Condition | Named Entities: SupplyTruck; BaseArea / Inside | The named truck's origin is inside |

Neither condition has child conditions. Targets and Requirement contain settings only. Radius and Polygon entities supply reusable shapes only; events, flags and logical groups remain in the framework. Reuse the same named area across different checks. The shared population snapshot, membership cache and one-second schedule serve both general and area counts; no new discovery, polling loop, world scans, spawn hooks or prefab tracking were added. Count and Proportion for the same population/area share their observations, including changes to the denominator.

Compatibility: old Area Condition `m_Count GC_AreaCount` is retained as hidden serialized data and used when the new `m_Requirement` is unset. An explicit new Requirement overrides it. To migrate external resources, rename that field to `m_Requirement`; keep the count values. The existing local scene's old Count remains readable and its other edits were preserved.

The previous prototype's area-based Living Characters configuration must move to Area Condition: copy Who/Factions into `m_Targets GC_CharacterAreaTargets`, copy old Area Name to `m_sArea`, Side to `m_bOutside` (0 = inside, 1 = outside), and keep the Count/Proportion object in `m_Requirement`. Living Characters now exposes Count only. Its original Requirement remains a hidden compatible reader: old Counts still apply until an explicit Count is chosen, and old Proportions are rejected, including when their Area was unset. The hidden legacy Area reader also rejects old area-based checks with a migration diagnostic. Neither can silently broaden to count everyone everywhere. The reload saved one previously unsaved Living Characters proportion entry from the local scene. It was migrated to Area Condition, preserving Players and AI and the proportion; its previously unset area remains unset. Other scene edits were preserved and are not included in the source commit. Remove the legacy Area after migrating; do not merely change its name to blank.

Verification: production script compilation/reload and independent source review. Optional GC_LivingCharactersTests.c covers shared filters/observations, proportion boundaries, invalid combinations, missing context, and legacy-count precedence. Optional tests and in-game movement/respawn/multiplayer checks were not executed at the user's request. Existing unrelated Arland resource/menu diagnostics remain.

### Normalized value sliders

All visible Lobby2 editor float settings whose domain is 0..1 use UIWidgets.Slider: event Random Chance, scheduled Random Chance, Casualty Ratio, Damage Amount, and area Required Proportion. Chance sliders use step 0.0001 (0.01 percentage points), preserving selections such as 0.0005 = 0.05%; other ratio sliders use step 0.01. Defaults, serialized field names and runtime validation are unchanged. This does not reinterpret existing saved values or convert legacy 0..100 percentage fields. General configurable float parameters, time and distance values retain their own ranges.

### Condition naming

Every selectable condition uses the visible name **[Name] Condition**: Area Condition, Casualties Condition, Count Condition, Entity Damage Condition, Game Mode State Condition, Group Condition, Interaction Condition, Living Characters Condition, Mission Event Condition, Mission Flag Condition, Random Chance Condition, and Total Deaths Condition. The nonselectable base remains **Do not use**. Serialized class names and field names are unchanged, so saved missions require no migration for this naming update. Targets and Requirement settings are not conditions and retain their short names.

### Mission interactions

Place **Prefabs/Logic/Mission/Interaction.et**. This is an empty, invisible-in-game entity with GC_MissionInteractionComponent, RplComponent, and a native ActionsManager context/action. The supplied context has Radius 2 and the action Duration 1 second. It uses the game's existing interaction menu and progress behavior, not a custom menu. Place it where players should interact; add visible scenery separately if wanted.

The component provides:

- **Action Name:** the player-facing text, such as Activate radio.
- **Factions:** optional allowed faction keys; empty allows every faction.
- **Required Flag:** optional existing Mission Flag that must currently be true. Put complex requirements on that flag in GC Lobby Manager. This is a gate, not a second condition list on the object. Flag memory remains true until Reset Flag is used.
- **Call Flag:** optional existing Mission Flag to call after use. Its normal conditions, count limit and repeatability apply. Rejection does not undo an otherwise accepted interaction.

Interactions work only during Match with an active authoritative Lobby2 mission framework. Living, conscious, connected player-controlled characters can use them; spectators, AI and dead/unconscious characters cannot. Availability is replicated by the component; interactions do not store a use count. Keep RplComponent on the same entity. The server verifies eligibility again at completion, and performs the mutation once; clients do not execute the effect.

To put this on an existing object, add **GC_MissionInteractionComponent** to that entity, keep/add its **RplComponent**, and add **GC_MissionInteractionAction** under its existing **ActionsManagerComponent > additionalActions**. Set the action's **ParentContextList** to an existing action context on that same entity, or create a native UserActionContext. Native context Position/Radius and action Duration own positioning/reach and hold time. Reuse existing components rather than adding a second ActionsManager. All GC interaction action slots on one entity share its component, label, restrictions; use separate child interaction entities for independent buttons. No changes to unrelated prefabs are required.

To observe use without flags, give the object a unique world name such as RadioSwitch. Add **Interaction Condition** to a mission event and enter RadioSwitch. Each accepted use makes the condition true for the current framework notification chain. The full condition tree is evaluated normally. After processing, the interaction becomes false again, whether those conditions passed or failed. A failed attempt can be retried; a later unrelated flag change cannot reuse that attempt. Reset sends no additional notification.

Event **Advanced > Repeatable** unchecked allows one run; checked allows later runs. The interaction itself remains usable unless its Required Flag blocks it. For lasting memory, have the successful event call a non-repeatable Mission Flag, or use Call Flag on the component when every accepted use should call that flag. To hide a prompt after success, use a persistent availability flag as Required Flag and clear it from the successful event. Flags remain optional.

Delay retains its normal requirement that conditions must still pass when the delay expires. An interaction pulse has already reset by then. To delay an interaction's outcome, let an immediate event set a persistent flag, and let the delayed event reference that flag.

Compatibility: the prototype's Has been used / Has not been used selector (`m_iMatch`) and component Repeatable setting (`m_bRepeatable`) are removed. Remove these saved fields in external interaction configurations. Existing Interaction Conditions now mean a current interaction signal, never remembered use or non-use; replace old memory checks with Mission Flag Condition. No saved Interaction Condition entries were found in the project's resources during this change. The removed replicated use count changes the component layout, so server and clients must run the same updated addon.

Place referenced interaction objects before Match starts. Names resolve at framework startup and retry at Match entry, then the resolved component is cached; there is no world scan or continuous name search. A missing component or deleted object is unavailable, including under None. Use a persistent flag if memory must survive deletion of the object. Duplicate world names are unsupported; the engine's named lookup is not a uniqueness scan.

Use notifications are processed together with the optional output flag call, preserving flag call ordering and existing loop guards. The same interpreter evaluates all conditions; the interaction object contains no nested framework or condition list. State changes update availability through an event subscription. The existing framework tick observes deletion and drains pending changes; no extra timer or frame callback is added. Stop removes condition listeners, and component deletion removes availability subscriptions. No global modded-class override is used.

Reference research: TilW checkout 4a5300f94fb9323ce4942ef296bcffcdda922626, scripts/Game/UserActions/TILW_BaseInteraction.c and Prefabs/Utils/Interactions/TILW_SetFlagInteraction.et, uses a native scripted action, optional faction/flag filters and single-use state. This implementation reuses the native action/context pattern with original Lobby2 code, authoritative component state, and the existing GC flag semantics; destructive parent deletion and teleport are outside this slice.

Installed engine evidence (catalogue revision 95a1918a7fed40bf884083293ae050d6): game/data/data007.pak/scripts/Game/generated/UserAction/ScriptedUserAction.c documents server-first verification and server-only/nonbroadcast effects (lines 40-53); BaseUserAction.c documents native context access and action-manager ownership. SCR_InteractionHandlerComponent.c lines 347-357 uses controlled-entity origin, context visibility range and squared-distance tolerance 1.1 for a range bound. The extra server bound uses that same calculation and only this action's registered contexts; native action handling still owns exact context eligibility/visibility. Component/player identity checks do not rely on UI visibility alone.

Verification: script compilation/reload and independent source review. Optional GC_InteractionTests.c covers authored input validation, unavailable conditions under None, repeated interaction notifications, reset after success or failure, rejection of stale interaction signals on later flag changes, stop behavior, and interaction plus flag dispatch. Those tests were compiled but not run. Native menu appearance, interaction input, resource instantiation, client/server transport and replication/JIP have not been tested in Play, per the requested no-in-game-test scope. Treat those as prototype verification limits, not established runtime results. Existing unrelated Arland resource/menu errors remain.

### True/false checkbox settings

Two-choice framework settings now use checkboxes. Flags default to false with zero successful calls; Start Call Count can initialize remembered calls. Flag Action owns calls and count changes; older Call Flag and Reset Flag objects require migration. **Require False** checked matches false; unchecked matches true. **Repeatable** checked allows repeated activations; unchecked allows one. **Outside** checked selects outside; unchecked selects inside. **Require Other State** checked matches another phase; unchecked matches the selected phase. **Roll Once** checked keeps the first roll; unchecked keeps rolling. **Count Flag Calls** checked uses the named flag count; unchecked counts true child conditions. **Pick One Random Action** checked picks one; unchecked runs the list.

These are boolean fields, as required by the engine checkbox widget. Saved external configurations must rename the following fields on the specified classes, retaining numeric values 0/1 and omitted defaults except the removed starting-value fields. Project examples and script consumers were updated. Invalid old numeric values are not boolean settings and must be corrected before conversion.

| Class | Previous field | Checkbox field |
| --- | --- | --- |
| GC_MissionFlag | m_iStartingValue / m_bStartsTrue | Removed; use Start Call Count or Flag Action > Call |
| GC_SetFlagAction | m_iSetTo | m_bSetTrue |
| GC_MissionFlagCondition, GC_MissionEventCondition | m_iMatch | m_bRequireFalse |
| GC_GameModeStateCondition | m_iMatch | m_bRequireOtherState |
| GC_Event / GC_RandomEventGroup (formerly GC_RuleOptions) | m_iRun | m_bRepeatable |
| GC_RandomChanceCondition | m_iRollBehavior | m_bRollOnce |
| GC_ConditionCount | m_iCountFrom | m_bCountFlagCalls |
| GC_AreaCondition | m_iCheck | m_bOutside |
| GC_LivingCharacterArea (legacy) | m_iSide | m_bOutside |
| GC_RuleRandom (legacy) | m_iActionSelection | m_bPickOneRandomAction |

Group operators and selectors with three or more choices retain dropdowns. Runtime enum values remain unchanged. Verification: script compilation/reload and source review only; no in-game tests.

### Successful-call flag verification

This update adds Reset Flag and makes counts authoritative for flag values. Source review covers zero/one/multiple calls, rejected calls, repeatability, ordered reset, same-chain loop guards and manager authority checks. Optional regression fixtures were updated for persistent memory and reset; they were not run in-game. Interaction and repeatable mission-event signals retain their own temporary behavior. Startup flags do not become true merely because their Conditions list is empty; their Start Call Count determines the initial value.

### Advanced event category

Every regular event, including children of random groups, and every Random Event Group has an **Advanced** category directly on the event. It contains Repeatable, Delay (seconds), Random Chance (0–1 slider) and Disabled. There is no separate Advanced object to add or select. Defaults remain one activation, no delay, 100% chance and enabled.

Workbench controls category expansion. The installed Attribute declaration supports category grouping but exposes no default-collapsed setting; this update does not force collapse or change global editor preferences. Category expansion and remembered layout are editor-owned. Live picker/layout verification is not claimed: the available Workbench 1.9 MCP entity-inspection operation reports unsupported. Compilation/reload passed; no in-game tests were run.

Migration: remove the `m_Advanced GC_RuleOptions { ... }` wrapper and move its `m_bRepeatable`, `m_fDelay`, `m_fRandomChance`, and `m_bDisabled` fields directly into the owning `GC_Event` or `GC_RandomEventGroup`. Preserve all values, including zero chance. Omitted fields retain their attribute defaults. The two saved project fixtures were converted; an empty local test-world options object was removed while preserving other local edits. No configured event options objects remained in the active Arland resources after the save/reload check. External prefabs with inherited Advanced overrides must migrate both parent and child resources. `GC_RuleOptions` is removed. Entity Damage's separate options are unaffected.

Script-created event fixtures must explicitly set `m_fRandomChance = 1` for full chance; script construction does not apply Workbench Attribute defaults. The optional fixtures were updated and source-reviewed, not executed. Native Attribute evidence: installed core scripts/Core/generated/Attributes/Attribute.c constructor (line 81), catalogue c52648a62fcb4229ad089610d060ea44, source hash e280dd320dea17bf88126507f257d4b4f449e0e86e6388624b2db6c47459c4a4.

### Start Call Count

Start Call Count is an integer from 0 to 1,000,000, default 0. Zero starts false; any positive value starts true. It seeds remembered successful calls without emitting a call notification. Normal startup checks still see the seeded value. A non-repeatable flag with a positive count cannot accept another call until Reset Flag; repeatable flags can accept more if their conditions pass. For example, starting at 4 with a count limit Less than 5 permits one additional call. Failed calls leave both count and value unchanged. Reset Flag clears to zero; restarting the framework restores the configured starting count. Existing saved flags omit this field and retain the zero default. Optional fixtures cover seeded memory, call limits, reset/restart and invalid values; no in-game tests were run.

## Flag catalog review

The supported flag vocabulary is deliberately small:

- **Mission Flag** owns remembered count and activation requirements. Keep it optional.
- **Flag Action** changes that memory or requests an activation.
- **Mission Flag Condition** reads true/false without changing anything.
- **Count Condition** reads the remembered count, including starting values and direct edits. A separate flag-state or flag-call-count condition would duplicate these.
- Random chance, casualties, total deaths, entity damage, area and interaction remain **conditions**, not extra flag types. Put them inside a Mission Flag only when persistent memory or a reusable named result is needed.

The follow-up action-catalog cleanup removes duplicate legacy action classes from production. GC_RuleRandom remains a separate legacy compiled-settings reader, not an action choice. The example declares its two flags and uses Flag Action. Current saved resources contain no removed action types; unrelated user-authored scene settings are preserved.

This cleanup changes catalog visibility, wording and the example, not flag evaluation or execution. Workbench compilation and source review are used; no in-game test is run.

## Action catalog migration

The production action catalog includes: **Flag Action**, **Message Action**, **Event Action**, **End Mission Action**, **Respawn Tickets Action**, **Respawn Wave Action**, **Reset Random Chance Action**, **Spawn Action**, **Delete Entities Action**, **Assign Waypoints Action**, **Time and Weather Action**, **JIP Action**, **Map Marker Visibility Action**, **Vehicle Fuel, Supplies and Repair Action**, **Timer Action**. Log output is part of Message Action; the old Log Message class is hidden from new authoring. The shared base is now `GC_ActionBase`, explicitly labeled **Action Base - Do not use**, rather than the misleading empty GC_MissionAction. Message Action's concrete class is `GC_MessageAction`; its optional Title changes the popup heading without changing the Mission source.

Flag Action already includes Call, Set count, Add, Subtract, Reset to zero and Restore starting count. Random selection stays with Random Chance Condition and Random Event Group because it needs probability/pool settings rather than the same single-flag arithmetic fields. End Mission stays separate from Message because outcome/faction settings are unrelated to message text and warning presentation.

The previous `visible/insertable` metadata was insufficient to establish that legacy subclasses disappear from this Workbench picker. The duplicate production classes have therefore been removed. Two script-only legacy interpreter inputs live in the optional test addon, without container/field attributes. They are not production actions. Runtime flags, action ordering and network/message behavior are unchanged.

For resources maintained outside this checkout, migrate before loading:

| Old class | Replacement |
| --- | --- |
| GC_CallFlagAction | GC_FlagAction, m_iOperation 0 (Call), same m_sFlagName |
| GC_ResetFlagAction | GC_FlagAction, m_iOperation 4 (Reset to zero), same name |
| GC_SetFlagAction | Declared flag: true maps to Call; false maps to Reset to zero. Old undeclared Set True was idempotent: declare that flag and use Set count (operation 1), amount 1 to preserve that behavior. |
| GC_RandomFlagAction / GC_SelectRandomFlagsAction | Redesign explicitly with random conditions/groups and Flag Action. Old failure/unselected paths cleared flags; preserve those resets explicitly if needed. Do not blindly rename these objects. |
| GC_SendTestEventAction | Interpreter/testing only; no normal authoring replacement. Use flags or Event Control for new mission logic. |
| GC_MissionMessageAction | GC_MessageAction; message/factions/warning fields unchanged |

The open scene was saved before scanning all local .et/.conf/.layer/.ent files; no removed or renamed concrete action class occurred. Production scripts and updated optional test inputs compiled through Reload. No Play tests were run. The MCP direct array-add operation is not ported to Workbench 1.9, so the rendered class-picker contents were not independently verified; source removal, reference scanning and compilation establish the catalog change.

## Related action groups

Event Action contains all controls for a named mission event or random group: Enable, Disable, Reset and Check now. Its serialized class remains `GC_EventControlAction` so existing resources continue loading. There are no separate enable-event, disable-event, reset-event or run-event action objects. Check now never bypasses conditions.

Flag Action groups flag operations; Respawn Tickets Action groups ticket arithmetic. Message Action delivers player notices, admin-only notices or server log text. Reset Random Chance targets a named condition, not an event, and remains separate so event controls do not acquire unrelated reset-name fields. End Mission is separate because it changes the game-mode phase and needs an outcome/faction. Log Message Action remains only as a hidden compatibility reader for existing saved resources.

## Spawning prototype

Spawn Action contains Prefab, AI Group and Crewed Vehicle setups. See [spawning settings, examples and limitations](Spawning.md).

## Entity and waypoint actions

Delete Entities and Assign Waypoints are separate actions. See [settings, examples and validation limits](EntityActions.md).

## Weather action

Time and Weather Action changes the clock, a named weather preset, or both. Independent Change Time and Change Weather checkboxes select the changes. World Editor > Plugins > GC > List weather presets shows every preset in the loaded map. See [settings and engine behavior](WeatherAction.md).

## Timer condition

Timer Condition starts in a selected game state or waits for a named Timer Action, counts a fixed or randomly chosen duration, then stays true until Timer Action changes it or the framework restarts. See [timing, nesting and repeatability](TimerCondition.md).

## Event and flag field order

Regular events declare Name, Conditions, Actions, then Advanced (Repeatable, Delay, Random Chance, Disabled). Random groups declare Name, Conditions, Pick Count, Events, then Advanced. Flags declare Name, Conditions, then Advanced (Start Call Count, Repeatable); flags do not gain an Actions list. Advanced attributes live on the concrete event classes so inherited fields no longer precede the main settings. Serialized names, types and defaults are unchanged; compilation uses one shared settings builder. Reload compilation passed; rendered editor ordering was not independently verified.

## Current Advanced objects

The former inline Advanced categories are now optional collapsible objects on regular events, random groups and flags, after their main fields. Events use GC_EventOptions; flags use GC_FlagOptions. Empty objects retain the normal defaults. See [settings, migration and verification](AdvancedOptions.md). This supersedes the earlier category-only layout described above.

## Wave respawns

Add **Respawn Wave Action** to an event's Actions. It runs once per event activation, only during Match and on the authoritative Lobby2 manager. No new background timer, world scan or per-frame check is added. Slots are resolved from the existing roster and each candidate is rechecked before respawning. Each player is attempted at most once per wave. Unknown faction keys reject the entire wave before any spawn or ticket change; duplicate faction keys do not duplicate players.

Only connected players with claimed KIA slots qualify. A dead player waiting in the Lobby2 spectator body remains eligible; unassigned spectators, AI, disconnected players and players controlling a living character are excluded. Missing original spawn anchors are skipped, with no fallback to a moving group's current position. A failed spawn or possession restores its reserved ticket and does not consume the player cap; other eligible players can still fill the wave. With Use Tickets enabled, a faction without tickets is skipped while other factions can continue. A valid wave with nobody eligible is a normal no-op. Existing parameter and TilW waves call the same implementation with tickets enabled, no cap and slot order.

Example: create a repeatable event with a Timer Condition named `reinforcement_wave`, starting in Match, with Seconds = 60. Add Respawn Wave Action (chosen factions, Maximum Players = 10, Use Tickets checked), followed by Timer Action targeting `reinforcement_wave` with Restart. The timer schedules the next attempt; the wave action itself does not repeat or add delays between players. Existing parameter-based respawn intervals are independent triggers: set them to zero for factions whose waves should be controlled only by this event.

Verification: the optional test addon's **GC Tests > Check respawn wave action** checks production authoring defaults, compiled settings, scope copying and invalid input without starting Play or changing the world. Actual spawning, possession, ticket refund during an engine failure and multiplayer timing require an in-game test; those were not exercised for this slice.

## Map marker and vehicle actions

Map Marker Visibility Action changes existing Lobby2 marker visibility by faction and for players without a faction. Vehicle Fuel, Supplies and Repair Action independently sets fuel, sets supplies and repairs named entities. See [settings, TilW coverage and verification](MarkerVehicleActions.md).

## JIP action

Add **JIP Action** to a mission event and choose **Manual**, **Automatic**, or **Disabled**. Manual lets eligible late joiners choose when to teleport. Automatic applies to newly issued eligible offers. Disabled denies JIP teleport requests; it does not block server connections or ordinary respawns. Conditions and Delay decide when the mode changes.

The action uses the existing server-owned JIP policy, without adding polling or changing its faction restrictions, two-minute join eligibility threshold, cutoff, or one-use records. Existing offers are not reissued on mode changes. Disabling can consume existing eligibility on the next normal JIP check; reenabling does not restore consumed or previously denied eligibility. A configured cutoff still closes JIP even in Manual or Automatic. It is a policy change, not a reset of player history.
