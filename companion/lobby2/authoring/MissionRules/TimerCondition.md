# Timer Condition

Add **Timer Condition** to an event or flag's Conditions list, including inside nested condition groups.

- **Timer Name:** unique name for Timer Action. Required when Start State is **Wait for Timer Action**; optional for automatic phase starts.

- **Start State:** Wait for Timer Action, Mission preview, Role selection, Briefing, Match (default), or After action.
- **Seconds:** typed decimal duration, default 60. For example, `2.5` is two and a half seconds.
- **Random Max Seconds:** optional typed decimal value, blank/default zero. Blank or zero uses Seconds exactly. A positive value chooses one duration between the two numbers when the timer starts. There is no Random Duration checkbox.

Both numeric controls use EditBox rather than SpinBox, and remain float fields. Workbench may display its unset float as 0; zero explicitly means no random range. Seconds must be between 0.1 and 86400. Random Max Seconds must be zero or between 0.1 and 86400. Negative, too-small positive, excessive and non-finite numeric values are rejected. Workbench owns text-to-float input handling. Reversed valid bounds are ordered automatically: Seconds 120 and Random Max Seconds 60 means 60-120 seconds. Equal bounds use a fixed duration without a random draw.

**Wait for Timer Action** stays stopped through every phase until an event runs **Timer Action > Start / Restart** or **Set time** targeting its name. A framework restart returns it to waiting. Other conditions in the same group do not start it. The timer can live in any event, flag, or nested condition group; its controller can be any event that supports ordinary actions.

For an automatic phase start, it starts once when the selected state is observed, unless Timer Action already started it. Its answer is false until the deadline, then stays true unless Timer Action changes it or the framework restarts. Counting continues through later states, so a timer started in Briefing can finish during Match. If the framework starts in the selected state, timing starts then; it does not reconstruct past state-entry times. If the selected state already passed before the framework started, the timer waits for that state to be entered. State changes and condition checks never reroll the deadline.

For example, name a timer `response_window`, choose **Wait for Timer Action**, and set its duration. In whichever event should begin the countdown, add **Timer Action**, select `response_window`, and choose **Start / Restart**. The timer's owning event or flag observes expiry normally. Repeated Start / Restart actions reset the countdown; use a once-only triggering event when appropriate. No objective-specific behavior is built into the timer.

## Timer Action

Use one **Timer Action** with **Timer Name**, **Operation**, and typed decimal **Seconds**. The name identifies one timer across events, flags, and nested condition groups. Missing or duplicate names reject the action without changing any timer; duplicate targets produce an explicit warning. Unnamed timers keep their existing automatic behavior.

| Operation | Behavior |
| --- | --- |
| Start / Restart | Start now with the configured duration; choose a new duration if random. Clear a finished result and leave Pause. |
| Pause | Freeze a started timer's remaining time. Repeated Pause preserves it. |
| Resume | Continue a paused timer. Repeated Resume does not extend its deadline. |
| Set time | Replace remaining time with Seconds. Also starts a waiting timer immediately. |
| Add time | Add Seconds to a started timer. Can reopen a finished timer. |
| Remove time | Subtract Seconds from a started timer, stopping at zero. |

Seconds is used only for Set/Add/Remove: 0 to 86400, including decimals. Negative, non-finite, excessive values and additions exceeding 86400 remaining seconds are rejected without mutation. Set/Add/Remove preserve Pause while time remains; zero finishes immediately, even while paused. Pause/Resume do not change a completed result. Pause/Resume/Add/Remove on a timer that has not started are rejected; use Start / Restart or Set time to start it. Actions change this run's remaining time, not the authored duration. Restart always uses the authored duration again. A later selected-state transition never overwrites an action-started timer.

Result changes notify the normal condition system. Every enclosing group and other condition still applies. The owning event's completed status and repeatability are unchanged: use Event Action > Reset separately if a once-only event should become available again. Timer Action does not cancel an event's already pending Delay. There are no new polling loops or global hooks; timing remains owned by the active framework engine.

Example: Start State Briefing, Seconds 60, Random Max Seconds 120 chooses one duration from one to two minutes after Briefing starts. Clear Random Max Seconds or set it to 0 for exactly 60 seconds. Combine the timer with Mission Flag `ready` inside All to require both. Neither bypasses the other. Use None around the timer for the opposite answer. Add a Game Mode State Condition if the event should only run in one particular phase.

The timer emits one expiry notification. It is not a repeating interval. Advanced Repeatable allows an event to run on later relevant notifications while the timer remains true; it does not restart the timer. Resetting an event also does not restart it. Use Game Mode State for an immediate state-entry condition, rather than a zero-second timer. Event Delay remains separate: it waits after the event's conditions pass.

Implementation uses timer leaf kind 12 in the existing per-engine scheduler. Fixed durations consume no random draw; a nonzero random range consumes one draw on automatic or action-driven start. Authored containers hold no live deadline or result. The existing manager scheduler runs every 100 ms of world simulation time; expiry is observed on its next update, not on every rendered frame. Pausing simulation pauses this timing. All due leaves update before dependent notifications. Random Chance Condition retains its separate Match-only rolling behavior.

Existing local Timer Conditions contain no explicit old random settings and keep their Seconds and Start State values. For external resources using the previous schema: remove `m_bRandomDuration`; when it was true, rename `m_fMaximumSeconds` to `m_fRandomMaxSeconds` (use 120 if the old maximum was omitted). When it was false/omitted, discard the old maximum and leave the new field absent/zero. Otherwise a previously unused maximum could accidentally enable randomness. Script-created fixtures must explicitly set their start state; Workbench applies the attribute's Match default to authored resources.

Verification: source/logic review and Workbench compilation/reload. Optional `GC_TimerConditionTests.RunTimerChecks` covers selected-state entry, fixed/random deadlines, no rerolls, cross-state timing, after-action timers, persistent results, nested flag gating, restart, late updates, invalid ranges, pause/resume, decimal adjustments, early start, immediate finish, reopening a finished timer, random restart, overflow rejection and missing/duplicate targets. The action-start extension additionally covers waiting through all phases, required names, nested event-driven start, random sampling, expiry notifications, framework restart, Set time, and invalid start values.

Compatibility: the serialized `m_eStartState` field, existing phase values and Match default remain unchanged. Only the new -1 value means Wait for Timer Action; it is not a game-mode phase. Timer Action operation 0 keeps its existing behavior and is now displayed as Start / Restart.

2026-11-11 validation: Workbench 1.9.0.37 Reload succeeded (operation e65b916c5d6a4342bc85e2e38a8c53f1; existing stale Arland GUID warnings). The optional GC_TestTimerConditions entry point executed the production interpreter in embedded Play: TimerConditions: checks=43 failures=0 (operation 0e2428a6c1df4cc1897998442859c4be). No mission rules or objectives were added to the authored world.
