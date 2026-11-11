# AI group conditions

Add **AI Group Condition** (`GC_AIGroupCondition`) to an event or mission flag's Conditions list. It uses the existing mission framework: the event still owns actions, delay and repeatability.

- **Group**: choose an existing named AI group from the world dropdown, or enter the exact name of a group that will spawn later. Select the group entity, not a soldier or vehicle. The picker reuses `GC_AIGroupChoices`, also used by Assign Waypoints. Unnamed entities need **Plugins > GC > Name unnamed entities** before string references can find them.
- **Mode**: None (initializing), Idle, Autonomous (combat movement), or Following waypoint. Default: Autonomous.
- **Continuous**: unchecked stays true after the first observed matching mode; checked follows the group's current mode and becomes false when it leaves that mode. Default: unchecked.

Example: choose a patrol group, leave Mode as Autonomous and Continuous unchecked, then add your reinforcement or message action to the event. To react each time the group enters that mode, check Continuous and make the event Repeatable. Existing All/Any/None condition groups work normally; None with Continuous can express "this group is not following a waypoint."

Observation begins at Match entry and samples the current mode immediately. Pre-match transitions are not remembered. Match exit and engine shutdown detach listeners and clear latches. A new match starts fresh. Missing/wrong-type groups, or groups without `SCR_AIGroupInfoComponent`, fail closed even under None; the engine retries direct name lookup once per second during Match. A deleted group makes continuous checks unavailable; a previously confirmed one-shot latch remains true until match exit. A replacement group with the same name can bind on the next retry.

This observes the engine's group control mode; it does not order AI to fight or move. Soldiers can fight while their group is still Following waypoint, especially on Defend waypoints. None is a short initialization state and may already have passed before observation starts.

## TilW behavior and integration

Reference: `TILW_ControlModeFlag.c` in Til-Weimann/tilw-mission-framework, installed commit `4a5300f94fb9323ce4942ef296bcffcdda922626`. Its target mode, initial sampling, one-shot latch, continuous mode tracking and native mode-change callback are supported here as a reusable condition, rather than a second flag system attached to every group.

Lobby2 deliberately observes only during Match. A continuous condition becoming false does **not** erase a remembered mission flag: flags retain the framework's existing memory behavior. Use the continuous condition directly for current-state gating. One-shot and repeatable event behavior also remain separate from condition latching.

One native listener is shared per configured group name. Bound mode changes are handled immediately; retries use direct named lookups on the existing engine tick. There are no world scans or `QueryEntitiesBySphere` calls. More than 256 compiled group conditions makes group checks unavailable with an error. The existing server-owned mission framework controls execution; no global engine override or client RPC was added.

Engine evidence: installed catalogue `c52648a62fcb4229ad089610d060ea44`, `scripts/Game/AI/Components/SCR_AIGroupInfoComponent.c` (enum and control-mode invoker), and `Prefabs/AI/Groups/Group_Base.et` (native info component).
