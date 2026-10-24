# Admin feature list

Retained and expanded 2026-10-22. All administrative features below require the current logged-in admin session, verified by the server for each operation. GM access or client UI visibility alone is insufficient. This is a planning list; no feature is marked implemented by these studies.

## Retained from Server Admin Tools study

- Player list, search and session history.
- Kick player from server.
- Persistent account-based ban with reason and confirmation.
- Ban list and unban.
- Server status: FPS, uptime and player counts; additional entity metrics as useful.
- GM grant/revoke remains a candidate if appropriate for our game mode.

## Additions to evaluate from PlayableSelector

- Respawn selected player at current position or original spawn position.
- Create a playable slot from an approved character template and location, or make a compatible existing character playable. A dedicated creation form would be our own feature, not an observed PlayableSelector form.
- Assign/reassign players to slots and force entry into the assigned character where appropriate.
- Free a slot without disconnecting the player.
- Lock/unlock a slot, a group, or all eligible slots; define occupied-slot handling explicitly.
- Pin/unpin a player assignment.
- Remove a playable slot, separately from deleting the character/entity; define occupied-player handling first.
- Vehicle availability is explicitly excluded.
- Mission stage and freeze-time controls, faction lock/balance overrides.
  During the freeze period, allow movement within designated staging areas and
  prevent firing (user-confirmed behavior).
- Spectator/admin support: follow a player, identity lookup, admin communication; ordinary social features retain their own permissions.

Rename/duplicate slots and a standalone group editor remain potential additions requiring design, not verified reference features. Automatic admin grants, public MOTD, scheduled announcements, external event APIs and RCON remain outside the proposed first implementation slice.

## Shared design requirements

One server-owned admin policy across UI, RPC, command and editor routes. Derive actor identity from the connection and resolve current target identities server-side. Recheck permissions and targets before delayed execution. Use stable slot identity and explicit availability/occupancy/pin state. Coordinate assignment, faction, group, readiness, voice and controlled-character changes as one operation. Return denial/failure/completion with correlated audit evidence. Confirm destructive actions and report persistence/spawn failure accurately. Preserve unrelated game modes.

## Evidence

- [Server Admin Tools study](../.local/admin-study/report.md).
- [PlayableSelector study](../.local/admin-study/lobby/report.md).
- Both are local source studies. Live multiplayer behavior, rendered UI, compatibility and performance remain unverified. Implementation requires MCP-driven production-handler tests before acceptance.

## Added implementation scope

- Admin-assigned priority speakers: while any priority speaker transmits, suppress ordinary speakers through Lobby2 VON integration. Multiple designated priority speakers may transmit concurrently.
- Server-authorized player voice mute/unmute, without Reforger social/mute scripting. Both moderation controls apply only to Lobby VON (including preview and briefing), not normal gameplay voice.
- Admin-only top chat and targeted/all-player admin message popup.
- Admin-only tools menu for player targeting and tool discovery, sharing authority and audit paths.
- Audit every administrative action, rejection and critical outcome through the central logger.

## Historical admin menu direction (2026-10-22; superseded below)

The user rejected the current admin-menu design and instructed us to move on.
Stop menu design/polish work in this implementation pass; retain it as an
unaccepted implementation surface. A separate future feature slice will redesign
and improve it. Continue underlying admin features and MCP behavior/multiplayer
validation. Passing handler tests does not establish acceptance of this menu.

## Admin voice-channel assignment (2026-10-23)

User-added scope: a logged-in admin can move another connected player into any
existing Lobby VON channel: Global, faction HQ, group, or personal. There is no
same-faction restriction on the administrative move, including during briefing.
This changes voice routing only, never the player's slot, faction or squad.
At this checkpoint the admin-menu redesign was deferred; expose this action through the shared
player-row context UI in preview, lobby and briefing.

Investigation against current sources:

- `GC_VoicePlayerRowComponent.OnClick` already opens the shared player context
  menu; `GC_Lobby_PreviewMenu` uses the shared player/voice tabs for briefing.
- `GC_VoiceChannelListComponent.ReconcileChannels` currently filters channels by
  the viewer's faction and hides Global for faction-assigned briefing players.
  An admin destination picker must enumerate all valid destinations separately
  from the viewer's listening-channel presentation. A moved player must still see
  their actual assigned channel even when outside ordinary self-service access.
- `GC_LobbyManager.SetPlayerVoiceChannel` is the canonical replicated assignment
  and radio-refresh operation. The new admin handler must call this owner after
  execution-time admin, target identity and destination validation, with central
  audit and correlated result. Do not reuse self-service permission restrictions.
- Existing faction/group reconciliation, briefing defaults, and personal-owner
  channel closure can replace a manual cross-faction assignment. Integrate the
  admin assignment provenance into the existing voice state and lifecycle rather
  than adding a competing assignment system. Preserve valid admin selections;
  explicitly handle removed channels and disconnected personal owners.
- `GC_LobbyPlayerController.ApplyLobbyRadio` stops local capture before retuning.
  Validate that moving a transmitting player releases their old channel/priority
  lease and does not alter mute or priority status.

Before UI implementation, save an in-context concept for the player action and
channel picker, then validate the rendered result against it. The picker should
show the selected player, current channel, and clearly named destinations grouped
by faction and personal owner, with a visible result/denial. Do not require the
rejected admin tools menu to perform this action.

Required evidence: authenticated cross-faction moves of each channel type in
preview/lobby/briefing; ordinary and logged-out rejection without mutation;
stale player/channel rejection and audits; replicated membership/radio tuning on
host and peer; unchanged slot/faction/group and moderation; active-transmission
retuning; topology/phase transitions; personal-owner departure and target
reconnect; unrelated-mode isolation. Implementation and validation are pending.

### Selected-player destination contexts (2026-10-23)

User refinement: admins select a player once; right-clicking another player
retargets direct actions, while right-clicking a voice channel or slot offers an
explicitly named move/assignment for the selected player. Keep direct player mute,
priority and channel-picker actions alongside destination actions. Selection is
client-local presentation state with stable identity checks, shared between player
and voice lists, cleared on disconnect/admin loss/game transition, never server
permission. Destination menus capture both selected identity and destination;
subsequent selection changes invalidate an already-open action. Server handlers
still independently authorize and validate each action.

Draft source now implements this route and selection highlighting. v1 is the
picker concept; `Art/References/admin-voice-channel/concept-v2.png` is the selected
player/destination-menu reference. The implementation has not compiled or rendered
because Workbench startup reports Steam initialization failure. Test both interaction
directions, selection changes while menus are open, occupied/locked slots, stale
identities, channel disappearance, logout, and preview/lobby/briefing continuity.
No implementation/visual acceptance or commit is claimed.

## Earlier admin menu redesign direction (superseded by full-screen goal below)

User explicitly reopened and superseded the earlier menu deferral. Replace the
convoluted presentation with a large window occupying most of the viewport: a
simple left tool/category list, one task-specific right panel. First tools are
Heal (selected player or all players) and Teleport (player to move and destination
player). Contextual player selection seeds the target. Future bans/gear spawning
are examples, not required new tools in this slice; do not add empty placeholders.
Preserve existing server handlers and contextual admin capabilities. Keep only
inputs relevant to the selected tool, clear source/destination wording, one main
action and visible outcome. Logged-in admin authority and central auditing apply.
Concept-first references and rendered independent review are required as before.

### Player selection control requirement (2026-10-23)

All player-selection fields use an expandable search menu/list, not a traditional
combo dropdown. Opening shows connected players; each edit immediately updates
results ranked by name match (exact, prefix, then partial/close matches). No Enter
or Search submission is required. A result displays name plus faction/role when
available, retains player ID and stable identity, and is revalidated on execution.
Apply consistently to Heal target, Teleport moving player and destination, and
other admin player-selection surfaces. Preserve separate source/destination state.
Empty search shows the available roster; no matches and disconnected selections
have explicit states. New search-v2 concept supersedes dropdown imagery in earlier
redesign references. The replacement layout remains a non-runtime draft until its
matching controller/search picker and tool handlers are integrated.

## Active full-screen rework goal — 2026-10-23

Latest user direction overrides the large-window reference: cover the entire
screen. A simple left tools column selects Heal or Teleport; the right side holds
only the active tool's context. No future-tool placeholders. Heal supports selected
players (multiple), everyone, and a fixed 15-metre radius around the admin's living
character. Teleport has clear From and To player inputs. Every player picker opens
the connected roster for browsing with an empty search, supports click selection,
and filters/ranks results on each edit. Carry existing context selection into the
appropriate field, retain stable identities, and distinguish no match/disconnect.

Acceptance: real execution-time logged-in-admin denial and success through MCP;
heal affected and unaffected characters for each scope, dead/unspawned handling,
boundary inclusion at 15 metres; teleport From/To direction, occupied-character
handling, safe destination and observed resulting position; disconnected/recycled
identities; correlated outcomes and central audits; multiplayer replication;
full-screen layout and live search interactions at representative viewport sizes;
independent rendered comparison against the new full-screen reference and final
whole-slice review. Compilation alone and dispatch success do not satisfy behavior.

The full-screen Heal/Teleport menu, shared searchable roster, server healing and
owner-executed/authority-observed teleport are integrated. Active visual references
are Lobby2 `Art/References/admin-tools-redesign/full-screen-v3.png` and
`teleport-v3.png`. Older framed drafts are historical. Native script reload has
succeeded in Workbench 1.8.0.13 with the optional Lobby2McpTests addon loaded.

Bounded embedded-host evidence in `.local/admin-implementation/` shows production
healing restored a damaged living character for selected/everyone/nearby scopes;
stale selection was rejected without healing; an unspawned spectator was skipped;
unauthorized and same-player teleport requests were rejected. Search ranking,
empty-query browsing, click selection, selection retention across no-match search,
separate From/To fields, scope labels and close/reopen were exercised through MCP.
These are single-player observations, not full scope/replication coverage.

The peer connected transiently but the native host rejected it because host and
peer shared the all-zero account identity (console lines 959-977 in Workbench
session `logs_2026-10-23_03-29-15`). Steam/account readiness needs confirmation.
Positive teleport, ordinary/logged-out peer authorization, multi-target healing,
15-metre boundary/exclusions, disconnect/recycle, representative roster load and
multiplayer replication remain unverified. Reload also reports the existing
ThermalProfile resource-leak assertion and BlockListApi leak; acknowledging the
dialog to finish compilation is not a clean stability pass. Saved compile-failure
and intermediate screenshots remain as failure evidence. Final rendered comparison,
whole-slice acceptance remains outstanding; no commit is claimed.

Independent review checkpoint: [admin-menu-independent-review.md](../.local/admin-implementation/admin-menu-independent-review.md)
reports **verification incomplete, not ready**. Prototype closeness passes at the
captured viewport, source responsibility/authority structure passes, and bounded
host behavior/cleanup passes. It found no confirmed new source defect. B1 requires
consequential multiplayer/role/scope/teleport evidence; B2 requires intended-use
UI/context/input/lifecycle coverage; B3 requires performance and clean stability
evidence. Reviewer-authored checks were mechanically executed by the primary on
the existing MCP connection because the reviewer lacked direct tool access; raw
results were independently inspected. Reuse that reviewer for the consolidated
recheck. Test Play/peers are stopped, original settings restored and normal Lobby2
reopened with the optional test addon unloaded (PID 35944).


### Multiplayer continuation (2026-10-23, 08:25 UTC)

The earlier all-zero-identity connection blocker cleared: a native Peer Tool client
joined with a distinct real identity. Workbench PID 17764 exercised production UI
and controller/server paths with two living players. Selected healing left the
unselected player injured; multi-selection and Everyone healed both. Nearby healing
included the peer at the observed 15m separation and excluded it at 15.1m. The
ordinary peer's own Heal and Teleport requests were rejected as not logged in,
with unchanged health/position and central audits. Teleport succeeded in both
directions; remote-owner and server positions agreed, and the menu displayed the
observed success. These are bounded two-player tests, not a full-load benchmark.

Two context regressions were fixed: Open Admin Tools now checks the identity
captured by the player/slot context, and Make playable passes the character's
replication-component ID. The latter first failed with rejected-stale-character;
the retest created slot 43, preserved position, disabled AI and rejected duplicate
conversion. Native MenuBack action-value delivery closed the menu and all popups.
Explicit lobby shutdown released the menu, popups and owned input/voice/camera
state. In an unrelated mode after Lobby2, opening Admin Tools left the menu
unchanged, with no admin menu or admin input context.

Raw evidence: MCP repository `.local/admin-implementation/` files
`admin-menu-continuation-input-context.json`, `admin-menu-multiplayer-session.json`,
`admin-menu-continuation-final.json`, `admin-menu-continuation-log.json`,
`admin-menu-two-player-heal.png`, `admin-menu-two-player-teleport.png`, and
`admin-menu-recheck-manifest.json`. The screenshots show a different viewport
aspect ratio from the earlier full-screen captures. Earlier failures are retained.

Verification remains incomplete. Native reload still asserts a ThermalProfile
resource leak; compilation after acknowledging it is not a clean stability pass.
Large rosters/long names, canonical performance, occupied/dead-character edge
cases and role-loss/in-flight transitions still lack full evidence. Stopping the
peer process did not immediately disconnect its server connection; the selected
peer was still valid when Apply ran. That attempt is not a passing stale-selection
test. Subsequent observation confirmed disconnection. Consolidated independent
review remains required; no completion, commit or publication is claimed.


Consolidated recheck: `admin-menu-independent-recheck.md` confirms the current
layout, ordinary-peer authorization denial, two-player tools, context repair,
native Back and bounded isolation evidence. Whole-task verification remains
incomplete; roster load/performance and consequential lifecycle/eligibility cases
remain open. Fresh probe-only evidence in
`admin-menu-leak-probe-baseline-complete.json` reproduces the same ThermalProfile
path and 160-byte resource entry with Lobby2 and Lobby2McpTests absent. This
establishes occurrence without Lobby2, not the native cause or a clean reload.
Cleanup is recorded in `admin-menu-recheck-cleanup.json`: normal Lobby2 PID14964
is loaded with only core/base/Lobby2; test clients and Play are stopped, original
peer settings restored, and optional test addons unloaded.


### Lifecycle and measurement continuation — 2026-10-23 08:54 UTC

The next MCP session verifies real kick/disconnect selection removal, disabled
Apply, stale-identity rejection and same-player reconnect without reselection.
Native seated-player and unavailable-landing Teleports reject without moving the
source. Normal Workbench Stop during a pending Teleport logs cancellation, not
success. Native death followed immediately by production Heal/Teleport requests
keeps the original character dead and stationary; audit records skip/rejection.
Subsequent spectator-target requests also leave observed positions unchanged.

Optional `GC_TestAdminDeadTarget(targetId, destinationId)` arranges native death
and invokes the real controller requests. `GC_TestAdminProfileStart(samples)` and
`GC_TestAdminProfileReport()` record bounded native ProfileData observations,
without changing menu actions, identities or permissions. Four 100-sample runs
at 100ms intervals compare briefing/Heal/Teleport/briefing in one two-player
session. Mean native frame times were 2.394/2.391/2.375/2.411ms. This is bounded
small-roster evidence, not individual-frame pacing, full resource/memory coverage
or large-roster acceptance. GPU timing returned zero and remains unavailable.

Raw evidence and limitations are in MCP workspace `.local/admin-implementation/`
`admin-menu-edge-final.json`, `admin-menu-edge-notes.md`, the four `profile*.txt`
files and `admin-menu-profile-summary.json`. The initial spectator-view sample
is preserved but excluded from the briefing comparison. Large-roster/long-name
workflow coverage and the consolidated independent recheck remain open. The
known probe-only ThermalProfile assertion remains an environment limitation;
no menu-specific leak or clean stability is established. No completion or commit
is claimed by this checkpoint.


## Admin menu workload continuation — 2026-10-23 09:13 UTC

The shared player picker snapshots native roster names once per refresh; Teleport now uses a fixed action label while From/To and the summary retain names. Optional WORKBENCH-only UI fixtures exercised 128 display rows in Heal, From and To, including ranked queries, no-match selection persistence, rejected fixture IDs, final-row scrolling, long field/summary text and repeated popup cleanup. MCP captures cover the smaller viewport and full-screen layout against the unchanged v3 concepts. These are display fixtures, not 128 connected players or renamed native players.

Raw native frame intervals, search/rebuild timings, source manifest and failures are recorded under MCP repo .local/admin-implementation/admin-roster-final.json and admin-roster-validation.md; preceding lifecycle evidence is admin-menu-edge-final.json. The second client in this latest session was rejected by the engine for duplicate identity, so prior distinct-native-identity multiplayer runs remain separately identified. The known ThermalProfile reload assertion and current log warnings remain recorded. No total process/GPU-memory or GPU-present timing claim is made. Consolidated independent recheck remains pending; this continuation alone does not establish acceptance.

## Admin menu resource comparison — 2026-10-23 09:36 UTC

The final menu recheck passes the v3 layout, searchable/browsable pickers, native two-player Heal/Teleport, authorization/audit, recovery and bounded UI workload checks. Its remaining resource finding is under recheck against `admin-blocklist-comparison-final.json` and `admin-blocklist-results.md` in the MCP evidence directory. Two menu-free Lobby2 runs with normal native identities did not emit BlockListApi. A separate process with only core/base/IsolationProbe reproduced the exact BlockListApi leak after initial compilation failure, repair/reload and stock Play with a zero native identity. Lobby2 was absent from its pre-Play native addon inventory; the temporary invalid test file was removed before Play. This is a comparable baseline, not a repaired engine leak or proof of its internal cause. No production changes were made for this diagnostic.

The retained ThermalProfile and BlockListApi warnings must not be described as clean reloads. Full process/GPU resources, GPU presentation timing, 128 connected clients and live microphone transport remain outside this menu slice's passing measurements. Historical checkpoint gaps above are superseded only by their specifically cited later evidence and the final independent verdict.
