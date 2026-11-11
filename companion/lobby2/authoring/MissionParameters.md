# Mission parameters

Open **Parameters** from the briefing index in Preview, Briefing, or the briefing view during play. Select either side to inspect its values. Apply sends one revision-checked draft; Revert restores the current server values. Switching sides requires applying or reverting an edited draft.

Authenticated admins can edit either side until the mission ends. The current commanding officer (the existing first sorted command slot) can edit their own side before the deadline. Empty, disconnected or KIA command slots do not delegate this permission to another player. Ordinary players can inspect both sides.

The deadline starts when BRIEFING begins. `GC_LobbyManager.m_iParameterLockSeconds` defaults to 180 seconds and is bounded to 30–3600. The panel displays the synchronized countdown; notifications announce the window, the final 30 seconds, and the lock. Starting GAME locks CO editing immediately. The timer neither advances nor delays the mission. Deadline enforcement runs on the authority even with the panel closed. POSTGAME is read-only for everyone.

The example mission supplies these values for US and USSR:

- **Respawn budget:** initial extra lives for that side at GAME start. Zero means one life unless mission scripts grant tickets. During GAME an admin change adjusts current tickets by the difference from the previous setting; reductions clamp at zero, and later increases grant the full difference.
- **Wave interval (min):** automatically respawn eligible connected KIA players at their saved starting positions while tickets remain. Zero leaves waves to mission scripts. Positive intervals run from mission start, then from the last wave; changing to an already-due interval permits a wave on the next tick.
- **Callsign**, **Mission orders**, and **Rules of engagement:** player-facing briefing instructions. These do not rename native radio groups, create scripted objectives, or restrict weapons.

The existing Lobby2/TilW ticket owner remains authoritative. Scripted ticket grants and explicit waves continue to use it. A parameter-controlled budget initializes that side's tickets at GAME start; omit that binding in missions whose framework alone owns the initial budget. Successful spawns consume one ticket; failed spawns refund it. Automatic waves do not replenish the budget.

Mission authors retain the typed parameter catalog. `m_eEffect = NONE` supplies a displayed mission value; existing mission scripts may consume it. `RESPAWN_BUDGET` and `RESPAWN_INTERVAL` require nonnegative integer definitions, unique per faction/effect. Interval maxima cannot exceed 1440 minutes. Owner faction keys remain mandatory; the Parameters view deliberately shows each owner's values to everyone. The old visibility metadata stays serialized for compatibility, but does not hide a side in this view.
