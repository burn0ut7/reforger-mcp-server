# Mission history ingestion

## First three hours and player reporting

Lobby2 names are gold while confirmed cumulative `playSeconds` is below 10,800.
This is the existing v3 connected-during-GAME clock, including spectators,
independent of population eligibility. Lobby, briefing and disconnected time do
not count. Admin red takes priority. Every shared Lobby2 name surface uses the
same authority-owned flag, including late-join snapshots. No lifetime totals or
database credentials are sent to clients.

Apply `migrations/003_player_history.sql` to existing databases (fresh databases
use `schema.sql`). Grant readers SELECT on `player_visits`, `player_history` and
`player_population_daily`; the bridge needs SELECT on `player_visits` and its
existing importer grants. Run one owner per server profile, using the SAME
PostgreSQL database on every server:

```powershell
dotnet StatsIngest.dll --serve-profile 'C:\server-profile'
```

The service imports the four existing outboxes and handles
`Lobby2PlayerHistory/*.request.ready` mailboxes every two seconds. This mode
replaces separate scheduled import jobs for that profile. It follows existing
acknowledgement/deletion rules: imported stats payloads are removed, while
history, log and telemetry archives remain. Do not point it at the development
proof's original profile if preserving all original stats files; use copies for
that workflow. No Windows service or schedule is installed automatically.

The game requests prior-mission totals every 15 seconds and adds the current
mission's live stats. Latest cumulative revisions are counted once; the current
mission is excluded from the database subtotal. Gold expires on the next
one-second game sample. Across servers, visibility also depends on their
30-second stats publication and importer cadence. Recording starts when native
persistent identity resolves. Unknown identity, unavailable/stale responses
(90 seconds), legacy filtered-time records, unfinished prior missions and
incomplete or missing prior GAME statistics withhold gold. A recorded visitor
with no stats is confirmed zero only when complete closed mission history proves
the mission never entered GAME. The underlying recorded playtime is still
queryable. An unimported local standard-history batch blocks responses rather than
silently treating missing history as zero. Resolve retained batches with the
existing ingestion diagnostics. Interrupted missions require explicit historical
reconciliation; they are not silently declared complete.

The mailbox is profile-local and bounded to one outstanding request per running
mission, up to 256 identities and 128 KiB per payload. Responses are atomically
published and bound to a unique request and mission. Treat profile access as
server authority. Remove abandoned request/response files only after their
mission is no longer running. Failed requests remain for retry/inspection;
monitor disk usage. The existing stats crash-loss window still applies.

Reporting examples:

```sql
SELECT count(*) AS total_unique_players FROM lobby2.player_history;
SELECT * FROM lobby2.player_population_daily ORDER BY day DESC;
SELECT player_id, first_seen_at, missions_visited, recorded_joins,
       recorded_play_seconds, unknown_playtime_missions, partial_stats_missions
FROM lobby2.player_history;
```

`first_seen_at` means first evidence in the retained database, not a claim about
activity before collection began. Reconnects add recorded joins, not unique
players. Daily counts use the UTC date of mission visits; they are not a
continuous-across-midnight DAU measure. `last_visit_at` is the latest recorded
visit start, not last disconnection. Missing timestamps are excluded from daily
counts and remain present in lifetime unique counts. Unimported or absent history
cannot be reconstructed. Optional telemetry joins these identities via its
existing `playerIdentity` and `missionId`; enabling it is unnecessary for player
counts or the gold-name feature.

## Mission instances and performance

`$profile:Lobby2History` contains cumulative mission-instance batches, including sessions that never reach GAME. They share `missionId` with player statistics and session logs. Records include the native mission name/author/description/scenario/world, persistent server identity and name, phase timestamps, faction names, commander identity intervals, and native outcome/end reason. GC website metadata tags are not collected. Missing native header/configuration values remain empty (notably in Workbench).

The server samples host FPS, connected player count and active AI count every ten seconds. Separate session and GAME summaries contain sample count, mean/min/max sampled FPS, samples below 15 FPS, sampled peak population/AI, and largest sampling gap. The session summary stops at mission completion; postgame waiting is excluded. FPS is the engine's last-ten-frame average: this is not client FPS, a frame-time trace, a percentile, or a time-weighted average. Sampled peaks can miss short spikes. `hostKind` distinguishes Workbench, listen and dedicated hosts.

History snapshots publish every 30 seconds, at completion, and at normal session shutdown. All history JSON stays on the server after import; only its ready marker is removed. Back up `server-id.txt` with the profile, keep it stable across restarts, and provision a separate profile identity for each server. An unreadable identity stops mission-history collection with an explicit error. Database/network work runs only in the external importer. A process crash can lose the unflushed interval and leave `closed=false`; do not present it as a confirmed completed archive. Normal shutdown before a result records `interrupted`. Native defeat/unknown endings retain their raw faction context without guessing a winner.

Reapply `schema.sql` with the migration account. Add SELECT/INSERT on `lobby2.mission_history_batches` to the importer's existing grants. Schedule the same importer against all three outbox folders, with one owner per folder:

```powershell
dotnet stats-ingest/StatsIngest.dll --validate 'C:\server-profile\Lobby2History'
dotnet stats-ingest/StatsIngest.dll --once 'C:\server-profile\Lobby2History'
```

Website/Discord readers should have SELECT on the required views, never write access to ingestion tables. The new views are `mission_history`, `mission_performance`, `mission_phases`, `mission_commanders`, `mission_results`, and `mission_attendance`. `mission_results` links each faction to its last observed GAME commander, retaining that person after death/disconnect; `mission_commanders` retains all observed command intervals (sampled once per second). `mission_attendance` includes recorded lobby joiners and GAME player records, deduplicated by persistent identity. `connected_during_game` includes spectators; `slotted_during_game` means a recorded role assignment, not proof of alive participation. Player totals keep the existing post-processing eligibility policy.

```sql
SELECT h.mission_id, h.mission_name, h.started_at, h.outcome,
       a.player_id, a.player_name, a.play_seconds, a.slotted_during_game
FROM lobby2.mission_history h
JOIN lobby2.mission_attendance a USING (mission_id)
WHERE a.player_id = 'player-identity'
ORDER BY h.started_at DESC;

SELECT h.mission_id, h.mission_name, p.*
FROM lobby2.mission_history h
JOIN lobby2.mission_performance p USING (mission_id)
WHERE h.status = 'completed' AND h.closed AND h.complete
  AND p.scope = 'game' AND p.host_kind = 'dedicated';
```

Imports can arrive in any order. Mission history completeness covers its own revision stream; inspect `session_log_status` and the player snapshot's completeness separately before treating attendance/events/player totals as complete. A result is not proof that all three streams arrived. No new database credentials or production service deployment are bundled with the addon.

Lobby2 writes cumulative player snapshots and append-only death events every 30 seconds to `$profile:Lobby2Stats`. A `.json.ready` marker publishes a verified payload. A separate process imports batches into PostgreSQL; database/network work never runs on the game thread.

PostgreSQL is the chosen history store: relational joins support players, missions and membership intervals; JSONB preserves the versioned source record. Website and Discord services should query these views through their own service accounts. Neither service should connect to the game server's runtime. Future replay files can use `missionId` and mission-relative seconds as their join keys and remain separate from these low-frequency records.

## Run

Requires .NET 10 and PostgreSQL. Apply `schema.sql` with a migration account. Give the importer account SELECT and INSERT on `lobby2.mission_batches`, plus USAGE on schema `lobby2`.

```powershell
dotnet publish companion/lobby2/integrations/StatsIngest/StatsIngest.csproj -c Release -o stats-ingest
# Supply LOBBY2_STATS_CONNECTION_STRING through the server's secret configuration.
dotnet stats-ingest/StatsIngest.dll --validate 'C:\server-profile\Lobby2Stats'
dotnet stats-ingest/StatsIngest.dll --once 'C:\server-profile\Lobby2Stats'
```

Run `--once` on a periodic service schedule. Exactly one importer owns the directory. Database commit precedes file deletion. Retrying an identical mission/revision is idempotent; a different checksum is rejected and retained. Invalid batches remain for operator inspection and do not prevent later valid batches from being processed. Never manually change a published payload under the same filename.

`--validate` validates the contract without a database or deleting batches. Payloads are limited to 64 MiB. Monitor retained files and free disk space. A crash can lose up to the last unflushed interval; the writer does not claim power-loss fsync guarantees. An interrupted mission stays non-final. Memory capacity overflow marks history incomplete; the public totals views exclude incomplete or unfinished missions.

## Query surfaces

- `missions`, `mission_players`: latest cumulative snapshot; never sum revisions.
- `player_totals`, `player_roles`: completed and complete mission histories.
- `deaths`: append-only victim/killer/weapon/distance events, including unknown attribution.
- `memberships`: faction, squad, leader, role and slot intervals with activity counters.
- `squadmates`: overlapping membership intervals in the same faction/group.
- `faction_totals`, `ammunition`, `faction_ammunition`: activity attributed to membership at collection time and projectile/magazine/weapon breakdowns.

Final totals are exposed only after all revisions from zero through the final revision have arrived. Personal identity and membership history are sensitive backend data: do not grant website visitors or Discord users direct database access. The in-game owner-only personal report does not publish another player's report; faction reports contain aggregates only.

## Collection semantics

Schema v3 records all connected time during native GAME as `playSeconds`, including spectators, regardless of population. Lobby time and disconnected gaps are excluded. Membership intervals own this clock, beginning when identity is available; role seconds remain sampled approximately once per second. Population change points share the mission clock and are reconciled by the sampler. `startedAt` anchors the mission in UTC. No five-minute eligibility requirement applies.

Eligibility is a historical query policy, not a collection filter. `population_intervals` exposes the saved counts; `eligible_play_seconds(mission_id, assignments, minimum_players)` intersects connected intervals with periods meeting the threshold (default 5). `player_totals` and `faction_totals` expose raw `play_seconds` separately from default-five `eligible_play_seconds`. Versions 1/2 retain only the old filtered time: raw time is NULL for those records, and `raw_playtime_record_count` identifies coverage in mixed histories. Their eligibility cannot be recalculated. Historical role seconds likewise have population-filtered semantics before v3.

Movement is sampled displacement, not an exact travelled path. Passengers do not receive driver/pilot distance. Teleport-sized jumps are discarded. Radio time is a client-reported estimate, bounded by elapsed server time; reports arrive about every ten seconds, with a two-second grace period at mission end. Speech spanning membership changes is apportioned by interval overlap, so faction/squad radio attribution is approximate. Disconnecting can lose the last unsent radio report. CPR remains deferred. Replay telemetry uses the optional recorder described below.

Shots are projectile-shot callbacks, with projectile, magazine and weapon resource names retained. Hand-thrown grenades have a separate counter and also contribute their grenade prefab/type to the ammunition breakdown; they do not increment shots. Mounted turret projectile callbacks use the same collection path. The session log additionally records individual shot and damage timestamps; trajectories, hit locations and accuracy remain out of scope. Player kills count other human players, including friendly fire; schema v2 also counts team kills separately as a subset, using the engine friendly-player relationship. Earlier v1 records have unknown team-kill counts (SQL NULL); death events retain the engine relationship code for later filtering. Unknown weapon attribution is empty, and unknown distance is -1. CO and squad-leader counts are one per mission if the role was observed; role selections record slots used during GAME, preserving the prefab role name and slot ID. Slot labels currently derive from the role prefab in Lobby2.

Membership records query native faction/group/leader ownership, closing an interval when those values or the selected role change. Teammates are reconstructed by overlapping intervals rather than copying a roster every second. Replay data can later join these records without changing their ownership.

The debrief is available during POSTGAME through the lobby entry and header. Lobby2 replaces its inherited stock end-screen component and sets its prefab auto-reload delay to -1 so the report remains readable; explicit server auto-reload configuration still takes precedence. Personal replies go only to the requesting controller's owner. Faction replies contain totals and distinct participant count, never the roster. The ammunition display lists up to 32 types with bounded labels and explicitly reports additional types; full retained breakdowns remain in history. Finalization pushes the personal report and every registered faction summary together to the owning client. Its controller caches these immutable reports, including empty/partial results, for immediate tab switches and menu reopening. A client missing reports requests the full set once; server bursts are paced to at most two per second per controller. Cache state is cleared outside POSTGAME and during presentation shutdown. Capacity loss is shown as a partial report.

The collector bounds population change points to 4096, unique mission players to 1024, membership intervals to 4096 total/256 per player, ammunition rows to 8192 total/128 per interval, pending death events to 256, and remembered death entities to 16384. Reaching these limits marks the mission incomplete instead of publishing complete lifetime totals. Disk snapshots still run on the game thread every 30 seconds; only network/database work is external. Size and timing should be measured on the actual dedicated server before a large event.

References: [PostgreSQL JSON types](https://www.postgresql.org/docs/current/datatype-json.html), [PostgreSQL Windows distributions](https://www.postgresql.org/download/windows/).

## Admin session logs

Admin Tools → Logs shows the current mission only. Search matches actor or victim name or identity, including disconnected players. The event filter separates team kills; “Deaths (all)” also includes kills and team kills. Live refreshes every two seconds; Paused holds the current page. Previous/Next browse 20 records at a time, with the current page number between them. Page 1 contains the latest matching events; changing filters, Refresh, or resuming Live returns to Page 1. The in-memory window holds the most recent 4096 records and explicitly directs admins to the server archive for earlier events.

Every request checks current server-side admin authorization. Repeated denied log-view requests are audited at most once per controller every five seconds; no log rows are sent to the denied requester. Filters are limited to 128 characters. Oversized audit details are explicitly truncated to fit the historical contract; the existing console audit output remains unchanged.

Damage against players and AI uses the same `damage` event type; `targetKind` distinguishes the victim. Every accepted damage callback is its own sequence-numbered event, even in the same engine tick. Healing/regeneration and the engine's empty duplicate impact notification are excluded; bleeding damage remains individual. `reportedDamage` is the engine context value, not a claim of final health removed. Shots and grenade throws are also individual events. Gameplay collection runs during GAME; connections and centrally logged admin actions span the session. No damage events are merged to reduce storage or network cost.

Damage records additionally snapshot `actorEntity`/`targetEntity` (session-local body IDs), prefab references, and `actorPosition`/`targetPosition` (world-space `[x,y,z]` origins in metres). Unknown positions are empty arrays; older batches may omit these additive fields. Actor/target identity, event sequence/time, reported damage/type, weapon/projectile when available, and distance are retained independently for each event. These snapshots support a future event timeline; they do not supply continuous movement, orientation, animation or precise impact geometry for full 3D playback.

Deaths carry `deathCause` (`respawn`, `bleeding`, `suicide`, or `other`), `creditSource` (`lastDamage`, legacy `lastPlayerDamage`, `engine`, or empty), and `damageEventSequence` (the originating session hit, or zero). Old records remain readable. Respawn is the accepted native respawn action, including leave-faction; suicide is a self-kill. Bleeding uses the native destroyed blood hit zone.

For respawn, bleed-out and suicide, the last external player or AI who dealt positive damage during that life receives statistical kill credit. The same resolution feeds counters, session events, persisted player deaths and telemetry's damage reference. Friendly hits retain team-kill classification. No native kick/penalty policy changes. There is no time expiry within a life; healing, self damage, bleed ticks and terminal TRUE notifications do not overwrite the last valid hit. Direct deaths retain engine attribution.

The existing event collector retains one immutable damage reference per victim body in a server-only lookup, capped at 16,384 entries. Lookup/update do not scan combat history. Death, body deletion and collection shutdown release entries; overflow marks statistics incomplete. Actor identity, faction, relation, weapon and hit distance survive attacker disconnect/deletion and activity-ring eviction. No new RPC, client cache, per-frame scan or persistence stream is added.

Existing player/faction totals automatically include these awarded kills and deaths-by-source. To aggregate causes or delayed credits separately, query existing `lobby2.deaths.detail` (player-involved deaths), or `lobby2.session_events.detail` (including AI-only deaths). For example:

```sql
SELECT detail->>'deathCause' AS cause,
       detail->>'creditSource' AS credit_source,
       detail->>'killerKind' AS killer_kind,
       count(*) AS deaths
FROM lobby2.deaths
GROUP BY 1, 2, 3;
```

AI faction counters remain match-local as designed; AI-only session events remain available for backend aggregation. No additional per-AI persistent profile or database schema migration is needed. Deploy the updated importer with the addon so `suicide` and `lastDamage` pass validation. Historical totals are not rewritten.

Combat counters are separate: `kills` retains its original player-kill meaning; `aiKills` counts AI victims. `deaths` is the player's total, with `deathsByPlayers` and `deathsByAI` as disjoint subsets; other deaths are the remainder, including uncredited respawns, suicides and environmental deaths. `teamKills` remains a subset of player kills. These counters flow through personal, membership and faction summaries. The debrief shows individual rows in a scrollable list and retains its client cache.

Death records identify `victimKind`/`killerKind` (`player`, `ai`, `other`) and prefab references; session records use `targetKind`/`actorKind` and corresponding prefab fields. AI references are mission-local `ai:` entity IDs, never player identities. The `aiKill` log type has its own admin filter and is also included in “Deaths (all)”. A disconnected tracked player body stays a player victim. AI classification uses native control types, not simply a missing player ID. Death weapon attribution uses the matching original damage event when available, never a later equipped weapon. AI-only deaths with no player involvement are outside these player statistics.

Reapply `schema.sql` to append AI/death-breakdown columns to player and faction views. Old archives remain readable; absent breakdown fields produce SQL NULL, not invented zeroes, and `combat_breakdown_record_count` identifies coverage. New records must include all three nonnegative breakdown counters, and their death subsets may not exceed total deaths.

The server publishes separate batches (up to 256 events each, at most four batches per second) to `$profile:Lobby2SessionLogs`. The queue retains up to 8192 records; disk failure retries the identical batch and reports unavailable storage. Overflow is explicitly counted as dropped history. Normal shutdown drains pending records and writes a closed marker; process crashes can lose unflushed records. Files are permanent: the importer removes only the `.ready` marker after commit and leaves session JSON on the server. There is no automatic deletion or retention cutoff. Back up the folder and monitor free disk space.

Apply the updated `schema.sql` with a migration account; it accepts v1 and v2 stats. Grant the ingestion account SELECT/INSERT on `lobby2.session_log_batches` and `lobby2.session_events`, plus its existing mission-batch grants. Run the same importer against the session log directory:

```powershell
dotnet stats-ingest/StatsIngest.dll --validate 'C:\server-profile\Lobby2SessionLogs'
dotnet stats-ingest/StatsIngest.dll --once 'C:\server-profile\Lobby2SessionLogs'
```

`session_events` stores one immutable JSONB row per `(session_id, sequence)`, indexed by actor, target and event type. Exact replay is idempotent; a conflicting event or batch rolls back and retains the local file. `session_log_status` exposes closure, dropped count and whether all batch revisions arrived. Session IDs match the mission stats ID; `sessionSeconds` starts at world load whereas `missionSeconds` starts at GAME. Their `startedAt` Unix timestamps provide the offset. A closed, contiguous stream with zero dropped records is the complete historical session; other streams must be presented as partial.

```sql
SELECT session_id, sequence, detail
FROM lobby2.session_events
WHERE detail->>'actor' = 'player-identity' OR detail->>'target' = 'player-identity'
ORDER BY session_id, sequence;
```

## Optional replay telemetry

Recording is disabled by default. On the active server `GC_SessionLogComponent`,
`SetTelemetry(requesterPlayerId, enabled, quotaMiB = 1024)` checks the current authenticated
admin through the production authority gate. `GetTelemetryReport()` reports identity,
closure/drop status and sampling/write time. These server-side methods can be called
through generic MCP runtime invocation in embedded Play. This slice adds no player
menu or remote-client start/stop RPC.

Schema 2 targets 5 Hz with compact animation inputs: position, Euler angles in
native degrees, velocity, stance, aim/look, lean, dynamic stance and
ADS/raised/reload/sprint/freelook flags. No bones, skeleton definitions, weapon
matrices or ragdolls are recorded. The viewer selects/blends compatible animations;
these inputs do not reproduce exact original animation frames. Positions, angles,
velocity and animation scalars are decoded to three decimal places. States are compared before binary table allocation: position/velocity/angular vectors
use a 0.001 magnitude threshold and lean/stance-height/movement scalars use 0.001,
relative to the last emitted state. Discrete flags/commands/stance remain exact.
Sub-threshold changes accumulate; a five-second heartbeat refreshes all values. Each emitted state is a
full snapshot, not a delta dependency chain. Equipment selection is a change event;
speed is derived from velocity. Gear, faction and ownership are checked every five
seconds; changes shorter than that interval may be missed.

Death records include final position/orientation, terminate sampling of that life,
and leave a marker or standard death pose in playback. Respawn gets a new ID.
A maximum of 256 active actors is separate from 256 bounded retired identity
entries, so successive lives no longer consume permanent sampling slots.

Discovery selects connected players. Characters involved in recorded damage/death
and occupied vehicles are also tracked. It does not survey every AI. Player shot and
grenade launches use the existing statistics callbacks while statistics collect.
AI firing, projectile flight, exact impacts and grenade detonation remain unsupported.
Damage is the engine's reported value, not inferred health removed. Unknown attribution
remains empty/zero. Header `actors: players` describes discovery; AI can appear through
combat events.

Sampling waits at the next due actor in its ordered cursor rather than scanning
all actors every frame, and yields after an actor when elapsed work
reaches 2 ms or queued text reaches 64 KiB. There is no 16-actor ceiling. Discovery
visits up to eight connected players per callback. One actor can exceed the soft
budget; sample rates remain targets. Writes occur at five seconds, 64 KiB or
384 queued records. Publication is synchronous and timed separately. No additional
client RPCs or database requests are introduced in the game.

Limits: 256 active actors, 4096 recent projectile identities, 8192 damage links,
512 queued records and 128 KiB encoded text. The authenticated start operation
accepts a recording quota of 64–8192 MiB (default 1024 MiB). Files already rotate as
bounded revision batches; the aggregate quota stops the recording explicitly rather
than silently dropping old history. It is not a global disk quota or retention
policy. Overflow/storage errors remain explicit. Failed publication retains its
pending payload in memory; shutdown makes a bounded retry and can leave an unsealed
recording. A new recording requires the previous one to be sealed. Operators must
manage total disk retention/free space. Actual 128-player multiplayer capacity must
still be measured; removing bones does not establish a server/client benchmark.

The local proof deployment is outside both repositories at
`C:\Users\Gray\Documents\Lobby2Data`. Its `Import.cmd` snapshots all four ready
outboxes and imports copies, preserving original game files. Credentials remain in
the deployment's restricted configuration directory. Asset conversion and both
playback viewers remain separate work. `native-resources:<recordingId>` denotes an
unresolved native resource capture, not an available browser asset manifest.

Telemetry stays separate from aggregate statistics and session audit events.
`missionId` is the existing session ID and joins those records. `recordingId` is a
fresh UUID for each start/stop interval; revision starts at zero. Use the existing
importer and credentials with a dedicated `Lobby2Telemetry` outbox. Apply
`schema.sql`; grant SELECT/INSERT on `lobby2.telemetry_batches`. Files are named
`<recordingId>-telemetry-<revision>.l2t`, published with a `.l2t.ready`
marker. The importer converts binary to the existing PostgreSQL JSONB payload.
Legacy JSON batches remain readable. Spool payloads remain after import; only acknowledged ready markers are
removed. Size is limited to 1 MiB and 1024 records per batch. These are storage
bounds, **not measured safe in-game write sizes**. The importer retains invalid,
conflicting or temporarily unsuccessful batches. Serializable database conflicts
are retried by running the importer again, using the same immutable files.

### Time, identity and resource contract

The envelope requires `world`, `gameVersion`, `assetManifestId`, `startedAt`,
`clock: "session-world-ms"`, `coordinates: "enfusion-world-metres"`, `sampleHz`,
`fromMs`, `untilMs`, `firstSequence`, `closed`, `stopReason`, cumulative
`droppedRecords`, `coverage`, and `records`. `startedAt` is the session UTC Unix
anchor in seconds. All replay timestamps are milliseconds from the **same native
world-time origin as session events**, including recordings enabled mid-mission;
never subtract independently rounded Unix anchors to align events.

Each record has `sequence`, `timeMs`, `type` and `data`. Sequence starts at one and
is contiguous across stored batches. The contract supports explicit gap records;
this collector stops on overflow and reports the rejected observation in
`droppedRecords` instead. Dropped observations do not consume sequence numbers.
Sampling rates are targets: intervals over one second set `discontinuity`, without
inventing missing frames. Times are nondecreasing and within
the batch interval. Batches may arrive out of order; adjacent sequences, timeline
ordering, fixed metadata, cumulative drop counts and terminal closure are checked
at database insertion. An empty terminal batch is allowed. `telemetry_recordings`
reports delivery/closure/drop status; it does not certify a decodable replay.

Entity IDs are recording-local, never reused, and identify one spawned life.
`ownership` changes the attached player identity without inventing a new body.
Projectile IDs are similarly unique. Native entity/replication IDs alone are not
sufficient across deletion and reuse. `sessionEventSequence` links to existing
audit events; zero means no known link. A death's `damageSequence` refers to a
telemetry damage record, or zero for unknown attribution. Missing IDs remain
unknown; the viewer must not guess the killer or invent an impact.

The asset manifest is supplied by the future asset pipeline and must resolve
versioned prefab/mesh/animation/skeleton resources. It is an identifier, not a
promise that game assets can already be loaded in a browser. Terrain, models,
attachments and animation resources need verified viewer mappings. The importer
stores these references; it does not download or execute them.

### Tracks and playback semantics

- `spawn`, `despawn`, `ownership`: entity lifetime, prefab, identity and faction.
  Emit a baseline for every selected entity already present when recording starts.
- `appearance`: monotonically revised faction, visual resource and native
  `SCR_PlayerArsenalLoadout` payload. Capture changes, not every position sample.
- `state`: `position`, `anglesDegrees`, world `velocity`, aim/look radians,
  stance, command, flags, lean, dynamic stance and movement amount. Stance follows
  native 0–2; flags are ADS=1, raised=2, reloading=4, sprinting=8, freelook=16.
  `previousSampleMs` is the last actual observation before this state. Hold the
  preceding state until that time, then interpolate to `timeMs`; do not interpolate
  across the entire silence between changed records. `discontinuity` prohibits
  interpolation on first capture, teleports and observation gaps over one second.
  Five-second heartbeats carry the same complete fields. Resolve Euler wrapping
  using shortest-arc rotations; do not linearly blend wrapped angle values.
- `equipment`: changed weapon prefab, native inventory weapon index and sights
  index, separate from animation states. `-1` indices and empty prefab mean none.
- Schema 1 remains readable for existing archived captures (matrix/speed and
  optional skeleton/pose records). Schema 2 rejects bones and redundant speed/root
  matrix fields. Apply `migrations/002_compact_telemetry.sql` to existing databases
  before deploying the new importer. Fresh databases use `schema.sql`.
- `seat`: entity-to-vehicle ID, native compartment type and seat index; empty
  vehicle and both indices -1 mean dismounted. Record vehicle roots separately with `vehicle-state` (position, anglesDegrees, world velocity, previousSampleMs and discontinuity); do not invent infantry stance values for vehicles.
- `shot`, `grenade`: launch origin, velocity, shooter, projectile and weapon
  resources. `projectile-sample` records observed flight. Initial velocity alone
  does not establish the complete path, ricochets, penetration or a final hit.
- `impact`, `detonation`: engine-event position/normal and known target. Never
  turn the last sampled projectile location into a verified impact.
- `damage`, `death`: directional attribution and existing audit links.
  `reportedDamage` preserves native callback semantics; optional `healthRemoved`
  is allowed only when independently measured. They are not interchangeable.
- `gap`: affected track, time interval, count and reason. Playback must expose
  missing evidence and avoid interpolating across an unknown interval.

`coverage` declares actors (`players` or `players-and-ai`), pose fidelity (`inferred`
for schema 2; `state` or `skeletal` for archived schema 1), and booleans for shots, impacts, damage, grenades, vehicles and gear.
Coverage is a producer declaration, not evidence checked against the game by the
importer. Record references (including skeleton counts and appearance revisions)
need a stateful decoder check before playback; the importer validates individual
records and batch continuity but does not pretend to have the viewer's assets.

### Remaining recorder work and performance gate

Use one authority-owned optional recorder with no runtime work when disabled.
Keep existing statistics enabled independently. Begin evaluation at 5 Hz for root
and semantic state, distributing capture over frames and preserving actual sample
times. Do not copy the full killed-from capture into every sample: the earlier
measured 6–7 ms for two actors is unsuitable as a 128-player sampling strategy.
Exact skeletal/ragdoll playback is deliberately outside this recorder.
Starting/stopping must create boundaries and baseline state, with authenticated
server authority for administrative changes.

Bound actor/projectile counts, per-frame capture work, queued bytes, encoded chunk
size and retained disk bytes. Reserve room for gap and terminal metadata. Do not silently discard
replaceable motion observations under pressure; report gaps or stop optional
recording on sustained overflow/storage failure without stopping statistics.
Do not silently label a partial recording complete. Native serialization/file
writes still run on the game thread; the existing verified outbox writes and
reads back a whole payload synchronously. Moving database work outside the game
does not remove that cost. No per-frame network broadcast to players is needed.

Before enabling production recording, measure disabled versus enabled dedicated
server frame-time distributions, capture/serialization/write duration, queue
high-water marks, drops, disk growth and client/network impact at 128 players plus
representative AI/vehicles and combat bursts. Include importer outage, disk-full,
restart, spawn/respawn/reconnect, mid-mission enable, late/missing chunks and rapid
start/stop. Verify player and AI shots, mounted weapons, missed shots/terrain
impacts, grenade trajectories/detonations and damage ownership independently.
The current player statistics hooks do not establish that full coverage.

Validation on 2026-10-27: Release build passed with zero warnings/errors; 26 synthetic CLI contract cases passed. A new isolated PostgreSQL test cluster accepted schema application twice and all 41 combined contract/integration cases, including idempotent retry, conflicting content, closure and sparse out-of-order sequence/time/drop checks. Readback distinguished a closed contiguous recording from an open recording missing revisions. The test cluster was stopped. Independent source review approved this ingestion-only increment. Live Reforger compilation/capture, asset conversion, playback and 128-player overhead remain untested; the configured MCP endpoint refused connection.

### Binary recording and PostgreSQL storage

The game records L2TB v1 by default. The external importer converts it to logical
schema-2 JSONB in `lobby2.telemetry_batches`. PostgreSQL is the replay store for
now. There is no separate archive service, compression worker, cache tier or
automatic retention policy. Binary files are the existing local delivery spool,
not a second playback storage system. Monitor that spool's disk use; its existing
per-recording quota and manual cleanup policy still apply.

Movement uses native bulk integer/float array writes, avoiding per-state JSON
serialization. Metadata, gear and infrequent events retain JSON inside each batch.
No compression or database work runs in the game. Cadence, change thresholds,
authority, quotas and disabled-by-default behavior remain unchanged.

`dotnet StatsIngest.dll --decode-telemetry capture.l2t capture.json` validates and
exports a batch without consuming it; the output must not already exist.
`--once OUTBOX` and `--validate OUTBOX` accept `.l2t.ready` and legacy `.json.ready`.
Do not re-encode an already imported recording and import it again: checksums
identify immutable delivery bytes, so that would correctly conflict.

The optional `--binary-benchmark JSON_FILE_OR_DIRECTORY OUTPUT_DIRECTORY` command
round-trips schema-2 captures and measures encoding, decoding and compression.
Supply a directory containing only telemetry JSON. Its `.l2t.br` files are
benchmark artifacts, not a deployed archive or ingestion format.
`web/telemetry-binary.mjs` is an independent decoder for format verification and
future consumers; the website can read JSON from PostgreSQL instead. The browser
module checks framing/numeric timelines; full semantics belong to the C# importer.

L2TB v1 layout (little-endian, no padding): six int32 words containing magic
`0x4254324C`, version `1`, header UTF-8 byte length, cold-event UTF-8 byte length,
character count and vehicle count. Then header JSON without `records`, cold JSON
array, character integer table, character float table, vehicle integer table,
and vehicle float table. Each table is row-major. Character integer rows are
`sequence, numericEntityId, flags, command`; vehicle rows omit command. Character
flags pack stance in bits 0-7, animation flags in bits 8-12, and discontinuity in
bit 16; vehicle flags contain only discontinuity in bit 0. IDs decode as `entity-N`.
Character float32 rows contain `timeMs, previousSampleMs, position[3],
anglesDegrees[3], velocity[3], aimRadians[3], lookRadians[3], lean, dynamicStance,
movement`. Vehicle float rows end after velocity. Character states cost 96 bytes;
vehicle states cost 56 bytes, excluding shared header/cold data. Clocks decode by
truncating to six decimals to match native JSON envelope boundaries; other floats
round to three decimals. Records merge by sequence and undergo existing contract
and continuity validation. Unsupported versions, invalid lengths, duplicate
sequences, invalid flags and nonfinite numbers are rejected. Bounds remain 1 MiB
and 1024 records per batch; native queues remain capped at 512 records.
