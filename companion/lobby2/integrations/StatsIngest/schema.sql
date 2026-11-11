-- Run once with a database migration account. The ingester needs SELECT/INSERT only.
CREATE SCHEMA IF NOT EXISTS lobby2;
CREATE TABLE IF NOT EXISTS lobby2.mission_batches (
    mission_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision >= 0),
    checksum text NOT NULL,
    payload jsonb NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (mission_id, revision),
    CONSTRAINT mission_batches_schema_version CHECK ((payload->>'schemaVersion')::integer IN (1, 2, 3)),
    CHECK ((payload->>'missionId')::uuid = mission_id),
    CHECK ((payload->>'revision')::integer = revision)
);

-- Latest cumulative snapshot wins. Summing successive batches would double count.
CREATE OR REPLACE VIEW lobby2.missions AS
SELECT DISTINCT ON (mission_id) mission_id, revision, payload, received_at
FROM lobby2.mission_batches
ORDER BY mission_id, revision DESC;

CREATE OR REPLACE VIEW lobby2.mission_players AS
SELECT mission_id, revision, (payload->>'final')::boolean AS finalized,
       ((payload->>'complete')::boolean AND
        (SELECT count(*) FROM lobby2.mission_batches b WHERE b.mission_id = missions.mission_id) = revision::bigint + 1)
       AS complete, player->>'identity' AS player_id,
       player->>'name' AS player_name, player AS stats,
       (payload->>'schemaVersion')::integer AS schema_version
FROM lobby2.missions CROSS JOIN LATERAL jsonb_array_elements(payload->'players') AS player;

-- Change points become half-open intervals; duplicate-time changes have zero duration.
CREATE OR REPLACE VIEW lobby2.population_intervals AS
SELECT mission_id, (point->>'fromSeconds')::double precision AS from_seconds,
       lead((point->>'fromSeconds')::double precision, 1, (payload->>'durationSeconds')::double precision)
           OVER (PARTITION BY mission_id ORDER BY ordinal) AS until_seconds,
       (point->>'players')::integer AS players
FROM lobby2.missions CROSS JOIN LATERAL jsonb_array_elements(payload->'population')
     WITH ORDINALITY AS changes(point, ordinal);

-- Policy is a query parameter. Missing raw history is NULL, never fabricated time.
CREATE OR REPLACE FUNCTION lobby2.eligible_play_seconds(mission uuid, intervals jsonb, minimum_players integer DEFAULT 5)
RETURNS double precision LANGUAGE sql STABLE AS $$
    SELECT CASE WHEN (m.payload->>'schemaVersion')::integer >= 3 AND minimum_players >= 0 THEN
        COALESCE((SELECT sum(GREATEST(0::double precision,
            LEAST(p.until_seconds, (a->>'untilSeconds')::double precision)
            - GREATEST(p.from_seconds, (a->>'fromSeconds')::double precision)))
        FROM lobby2.population_intervals p CROSS JOIN LATERAL jsonb_array_elements(intervals) a
        WHERE p.mission_id = mission AND p.players >= minimum_players), 0::double precision)
        END
    FROM lobby2.missions m WHERE m.mission_id = mission
$$;

CREATE OR REPLACE VIEW lobby2.player_totals AS
SELECT player_id, count(*) AS missions,
       sum(CASE WHEN schema_version >= 3 THEN (stats->>'playSeconds')::double precision END) AS play_seconds,
       sum((stats->>'shots')::bigint) AS shots,
       sum((stats->>'grenades')::bigint) AS grenades,
       sum((stats->>'bandagesOthers')::bigint) AS bandages_others,
       sum((stats->>'kills')::bigint) AS kills,
       sum((stats->>'deaths')::bigint) AS deaths,
       sum((stats->>'walkMetres')::double precision) AS walk_metres,
       sum((stats->>'driveMetres')::double precision) AS drive_metres,
       sum((stats->>'pilotMetres')::double precision) AS pilot_metres,
       sum((stats->>'commanderMissions')::integer) AS commander_missions,
       sum((stats->>'leaderMissions')::integer) AS squad_leader_missions,
       sum((stats->>'radioSecondsClientReported')::double precision) AS radio_seconds_client_reported,
       sum((stats->>'teamKills')::bigint) AS team_kills,
       count(*) FILTER (WHERE stats ? 'teamKills') AS team_kill_record_count,
       sum((stats->>'aiKills')::bigint) AS ai_kills,
       sum((stats->>'deathsByPlayers')::bigint) AS deaths_by_players,
       sum((stats->>'deathsByAI')::bigint) AS deaths_by_ai,
       sum((stats->>'deaths')::bigint - (stats->>'deathsByPlayers')::bigint - (stats->>'deathsByAI')::bigint) AS other_deaths,
       count(*) FILTER (WHERE stats ? 'aiKills') AS combat_breakdown_record_count,
       count(*) FILTER (WHERE schema_version >= 3) AS raw_playtime_record_count,
       sum(CASE WHEN schema_version >= 3 THEN lobby2.eligible_play_seconds(mission_id, stats->'assignments', 5)
           ELSE (stats->>'playSeconds')::double precision END) AS eligible_play_seconds
FROM lobby2.mission_players WHERE finalized AND complete GROUP BY player_id;

CREATE OR REPLACE VIEW lobby2.deaths AS
SELECT mission_id, (death->>'sequence')::integer AS sequence, death AS detail
FROM lobby2.mission_batches CROSS JOIN LATERAL jsonb_array_elements(payload->'deaths') AS death;

CREATE OR REPLACE VIEW lobby2.player_roles AS
SELECT player_id, role->>'prefab' AS role_id,
       sum((role->>'selections')::integer) AS selections,
       sum((role->>'seconds')::double precision) AS seconds
FROM lobby2.mission_players CROSS JOIN LATERAL jsonb_array_elements(stats->'roles') AS role
WHERE finalized AND complete GROUP BY player_id, role->>'prefab';

-- Membership is an interval, so reconnects and mid-mission group changes remain distinguishable.
CREATE OR REPLACE VIEW lobby2.memberships AS
SELECT mission_id, player_id, player_name, finalized, complete,
       assignment->>'faction' AS faction_id, assignment->>'groupId' AS group_id,
       assignment->>'groupName' AS group_name, assignment->>'leader' AS leader_id,
       assignment->>'role' AS role_id, (assignment->>'slotId')::integer AS slot_id,
       (assignment->>'fromSeconds')::double precision AS from_seconds,
       (assignment->>'untilSeconds')::double precision AS until_seconds,
       assignment AS stats, schema_version
FROM lobby2.mission_players CROSS JOIN LATERAL jsonb_array_elements(stats->'assignments') AS assignment;

CREATE OR REPLACE VIEW lobby2.faction_totals AS
SELECT mission_id, faction_id, count(DISTINCT player_id) AS participants,
       sum(CASE WHEN schema_version >= 3 THEN (stats->>'playSeconds')::double precision END) AS play_seconds,
       sum((stats->>'shots')::bigint) AS shots,
       sum((stats->>'grenades')::bigint) AS grenades,
       sum((stats->>'bandagesOthers')::bigint) AS bandages_others,
       sum((stats->>'kills')::bigint) AS player_kills,
       sum((stats->>'deaths')::bigint) AS deaths,
       sum((stats->>'walkMetres')::double precision) AS walk_metres,
       sum((stats->>'driveMetres')::double precision) AS drive_metres,
       sum((stats->>'pilotMetres')::double precision) AS pilot_metres,
       sum((stats->>'radioSecondsClientReported')::double precision) AS radio_seconds_client_reported,
       sum((stats->>'teamKills')::bigint) AS team_kills,
       count(*) FILTER (WHERE stats ? 'teamKills') AS team_kill_record_count,
       sum((stats->>'aiKills')::bigint) AS ai_kills,
       sum((stats->>'deathsByPlayers')::bigint) AS deaths_by_players,
       sum((stats->>'deathsByAI')::bigint) AS deaths_by_ai,
       sum((stats->>'deaths')::bigint - (stats->>'deathsByPlayers')::bigint - (stats->>'deathsByAI')::bigint) AS other_deaths,
       count(*) FILTER (WHERE stats ? 'aiKills') AS combat_breakdown_record_count,
       count(*) FILTER (WHERE schema_version >= 3) AS raw_playtime_record_count,
       sum(CASE WHEN schema_version >= 3 THEN lobby2.eligible_play_seconds(mission_id, jsonb_build_array(stats), 5)
           ELSE (stats->>'playSeconds')::double precision END) AS eligible_play_seconds
FROM lobby2.memberships WHERE finalized AND complete AND faction_id <> ''
GROUP BY mission_id, faction_id;

CREATE OR REPLACE VIEW lobby2.ammunition AS
SELECT mission_id, player_id, faction_id, group_id,
       ammo->>'projectile' AS projectile, ammo->>'magazine' AS magazine,
       ammo->>'weapon' AS weapon, sum((ammo->>'rounds')::bigint) AS rounds
FROM lobby2.memberships CROSS JOIN LATERAL jsonb_array_elements(stats->'ammo') AS ammo
WHERE finalized AND complete GROUP BY mission_id, player_id, faction_id, group_id,
       ammo->>'projectile', ammo->>'magazine', ammo->>'weapon';

CREATE OR REPLACE VIEW lobby2.faction_ammunition AS
SELECT mission_id, faction_id, projectile, magazine, weapon, sum(rounds) AS rounds
FROM lobby2.ammunition GROUP BY mission_id, faction_id, projectile, magazine, weapon;

-- Use half-open intervals: touching membership changes do not imply shared squad time.
CREATE OR REPLACE VIEW lobby2.squadmates AS
SELECT a.mission_id, a.player_id, b.player_id AS squadmate_id, a.faction_id, a.group_id,
       greatest(a.from_seconds, b.from_seconds) AS from_seconds,
       least(a.until_seconds, b.until_seconds) AS until_seconds
FROM lobby2.memberships a JOIN lobby2.memberships b
  ON a.mission_id = b.mission_id AND a.group_id = b.group_id AND a.faction_id = b.faction_id
 AND a.player_id <> b.player_id AND a.group_id <> ''
 AND a.from_seconds < b.until_seconds AND b.from_seconds < a.until_seconds
WHERE a.finalized AND a.complete AND b.finalized AND b.complete;

-- Upgrade the schema-version check in databases initialized before team-kill counters.
DO $$
DECLARE constraint_name text;
BEGIN
    FOR constraint_name IN SELECT conname FROM pg_constraint
      WHERE conrelid = 'lobby2.mission_batches'::regclass AND contype = 'c'
        AND pg_get_constraintdef(oid) LIKE '%schemaVersion%'
    LOOP
      EXECUTE format('ALTER TABLE lobby2.mission_batches DROP CONSTRAINT %I', constraint_name);
    END LOOP;
    ALTER TABLE lobby2.mission_batches ADD CONSTRAINT mission_batches_schema_version
      CHECK ((payload->>'schemaVersion')::integer IN (1, 2, 3));
END $$;

CREATE TABLE IF NOT EXISTS lobby2.session_log_batches (
    session_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision >= 0),
    checksum text NOT NULL,
    payload jsonb NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (session_id, revision),
    CHECK (payload->>'kind' = 'sessionEvents'),
    CHECK ((payload->>'schemaVersion')::integer = 1),
    CHECK ((payload->>'sessionId')::uuid = session_id),
    CHECK ((payload->>'revision')::integer = revision)
);
CREATE TABLE IF NOT EXISTS lobby2.session_events (
    session_id uuid NOT NULL,
    sequence integer NOT NULL CHECK (sequence > 0),
    detail jsonb NOT NULL,
    PRIMARY KEY (session_id, sequence),
    CHECK ((detail->>'sequence')::integer = sequence)
);
CREATE INDEX IF NOT EXISTS session_events_actor ON lobby2.session_events ((detail->>'actor'), session_id, sequence);
CREATE INDEX IF NOT EXISTS session_events_target ON lobby2.session_events ((detail->>'target'), session_id, sequence);
CREATE INDEX IF NOT EXISTS session_events_type ON lobby2.session_events ((detail->>'type'), session_id, sequence);
CREATE OR REPLACE VIEW lobby2.session_log_status AS
SELECT session_id, max(revision) AS latest_revision,
       bool_or((payload->>'closed')::boolean) AS closed,
       max((payload->>'droppedRecords')::integer) AS dropped_records,
       count(*) = max(revision)::bigint + 1 AS all_batches_received
FROM lobby2.session_log_batches GROUP BY session_id;

-- Mission-instance metadata shares mission_id with player batches and session_id with events.
-- Separate cumulative revisions also preserve sessions which never reached GAME.
CREATE TABLE IF NOT EXISTS lobby2.mission_history_batches (
    mission_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision >= 0),
    checksum text NOT NULL,
    payload jsonb NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (mission_id, revision),
    CHECK (payload->>'kind' = 'missionHistory'),
    CHECK ((payload->>'schemaVersion')::integer = 1),
    CHECK ((payload->>'missionId')::uuid = mission_id),
    CHECK ((payload->>'revision')::integer = revision)
);

CREATE OR REPLACE VIEW lobby2.mission_history AS
SELECT DISTINCT ON (mission_id) mission_id, revision,
       payload->>'serverId' AS server_id, payload->>'serverName' AS server_name,
       payload->>'hostKind' AS host_kind, payload->>'missionName' AS mission_name,
       payload->>'author' AS author, payload->>'description' AS description,
       payload->>'world' AS world, payload->>'scenarioPath' AS scenario_path,
       to_timestamp((payload->>'startedAt')::bigint) AS started_at,
       to_timestamp(NULLIF((payload->>'endedAt')::bigint, 0)) AS ended_at,
       payload->>'status' AS status, payload->>'outcome' AS outcome,
       (payload->>'endReason')::integer AS end_reason,
       payload->'winningFactions' AS winning_factions,
       (payload->>'closed')::boolean AS closed,
       ((payload->>'complete')::boolean AND
        (SELECT count(*) FROM lobby2.mission_history_batches b WHERE b.mission_id = h.mission_id) = revision::bigint + 1) AS complete,
       payload, received_at,
       payload->>'endTitle' AS end_title, payload->>'endMessage' AS end_message
FROM lobby2.mission_history_batches h
ORDER BY mission_id, revision DESC;

CREATE OR REPLACE VIEW lobby2.mission_performance AS
SELECT mission_id, server_id, host_kind, phase.key AS scope,
       (phase.value->>'samples')::integer AS samples,
       CASE WHEN (phase.value->>'samples')::integer > 0 THEN (phase.value->>'meanSampleFps')::double precision END AS mean_sample_fps,
       CASE WHEN (phase.value->>'samples')::integer > 0 THEN (phase.value->>'minimumSampleFps')::double precision END AS minimum_sample_fps,
       CASE WHEN (phase.value->>'samples')::integer > 0 THEN (phase.value->>'maximumSampleFps')::double precision END AS maximum_sample_fps,
       (phase.value->>'samplesBelow15Fps')::integer AS samples_below_15_fps,
       (phase.value->>'peakSampledPlayers')::integer AS peak_sampled_players,
       (phase.value->>'peakSampledActiveAI')::integer AS peak_sampled_active_ai,
       (phase.value->>'largestSampleGapSeconds')::double precision AS largest_sample_gap_seconds
FROM lobby2.mission_history
CROSS JOIN LATERAL jsonb_each(jsonb_build_object('session', payload->'sessionPerformance', 'game', payload->'gamePerformance')) phase;

CREATE OR REPLACE VIEW lobby2.mission_phases AS
SELECT mission_id, phase->>'phase' AS phase,
       (phase->>'sessionSeconds')::double precision AS session_seconds,
       to_timestamp((phase->>'at')::bigint) AS at
FROM lobby2.mission_history CROSS JOIN LATERAL jsonb_array_elements(payload->'phases') phase;

CREATE OR REPLACE VIEW lobby2.mission_commanders AS
SELECT mission_id, commander->>'faction' AS faction,
       commander->>'factionName' AS faction_name, commander->>'identity' AS player_id,
       commander->>'name' AS player_name,
       (commander->>'fromSeconds')::double precision AS from_seconds,
       (commander->>'untilSeconds')::double precision AS until_seconds
FROM lobby2.mission_history CROSS JOIN LATERAL jsonb_array_elements(payload->'commanders') commander;

-- Join events include lobby-only visitors; GAME player snapshots also include connected spectators.
-- Use identity rather than a temporary network player ID. Reconnects do not create new people.
CREATE OR REPLACE VIEW lobby2.mission_attendance AS
WITH events AS (
    SELECT session_id AS mission_id, detail->>'actor' AS player_id,
           (array_agg(detail->>'actorName' ORDER BY sequence DESC))[1] AS player_name,
           count(*) FILTER (WHERE detail->>'type' = 'joined') AS recorded_joins,
           min((detail->>'sessionSeconds')::double precision) FILTER (WHERE detail->>'type' = 'joined') AS first_join_seconds,
           max((detail->>'sessionSeconds')::double precision) FILTER (WHERE detail->>'type' = 'left') AS last_leave_seconds
    FROM lobby2.session_events
    WHERE detail->>'type' IN ('joined','left') AND detail->>'actor' <> ''
    GROUP BY session_id, detail->>'actor'
)
SELECT COALESCE(e.mission_id, p.mission_id) AS mission_id,
       COALESCE(e.player_id, p.player_id) AS player_id,
       COALESCE(p.player_name, e.player_name) AS player_name,
       COALESCE(e.recorded_joins, 0) AS recorded_joins, e.first_join_seconds, e.last_leave_seconds,
       p.player_id IS NOT NULL AS connected_during_game,
       CASE WHEN p.schema_version >= 3 THEN (p.stats->>'playSeconds')::double precision END AS play_seconds,
       EXISTS (SELECT 1 FROM jsonb_array_elements(p.stats->'assignments') a WHERE a->>'role' <> '') AS slotted_during_game
FROM events e FULL JOIN lobby2.mission_players p USING (mission_id, player_id);


CREATE OR REPLACE VIEW lobby2.mission_results AS
SELECT h.mission_id, f.value->>'key' AS faction, f.value->>'name' AS faction_name,
       h.outcome, h.end_reason,
       CASE WHEN h.outcome = 'victory' THEN h.winning_factions ? (f.value->>'key') END AS is_winner,
       commander.player_id AS last_game_commander_id,
       commander.player_name AS last_game_commander_name
FROM lobby2.mission_history h
CROSS JOIN LATERAL jsonb_array_elements(h.payload->'factions') AS f(value)
LEFT JOIN LATERAL (
    SELECT c.player_id, c.player_name
    FROM lobby2.mission_commanders c
    WHERE c.mission_id = h.mission_id AND c.faction = (f.value->>'key')
      AND c.until_seconds > (SELECT min(session_seconds) FROM lobby2.mission_phases p
                             WHERE p.mission_id = h.mission_id AND p.phase = 'GAME')
    ORDER BY c.from_seconds DESC LIMIT 1
) commander ON true;


-- Optional replay stream. A new recording ID identifies each enable/disable interval.
-- Import order is independent of mission statistics; mission_id is a join key,
-- not a foreign key requiring low-frequency stats to arrive first.
CREATE TABLE IF NOT EXISTS lobby2.telemetry_batches (
    recording_id uuid NOT NULL,
    revision integer NOT NULL CHECK (revision >= 0),
    checksum text NOT NULL,
    payload jsonb NOT NULL,
    mission_id uuid GENERATED ALWAYS AS ((payload->>'missionId')::uuid) STORED,
    received_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (recording_id, revision),
    CHECK (payload->>'kind' = 'telemetry'),
    CONSTRAINT telemetry_schema_version CHECK ((payload->>'schemaVersion')::integer IN (1, 2)),
    CHECK ((payload->>'recordingId')::uuid = recording_id),
    CHECK ((payload->>'revision')::integer = revision)
);
CREATE INDEX IF NOT EXISTS telemetry_batches_mission ON lobby2.telemetry_batches (mission_id, recording_id, revision);

-- Delivery status is not proof of valid assets, resolved references or faithful animation.
CREATE OR REPLACE VIEW lobby2.telemetry_recordings AS
SELECT recording_id, mission_id, max(revision) AS latest_revision,
       bool_or((payload->>'closed')::boolean) AS closed,
       max((payload->>'droppedRecords')::integer) AS dropped_records,
       count(*) = max(revision)::bigint + 1 AS all_batches_received,
       min((payload->>'fromMs')::double precision) AS from_ms,
       max((payload->>'untilMs')::double precision) AS until_ms
FROM lobby2.telemetry_batches GROUP BY recording_id, mission_id;
-- Read models over authoritative history. No second player registry or telemetry dependency.
CREATE OR REPLACE VIEW lobby2.player_visits AS
WITH anchors AS (
    SELECT session_id AS mission_id, min((payload->>'startedAt')::bigint) AS started_at
    FROM lobby2.session_log_batches GROUP BY session_id
)
SELECT a.*, to_timestamp(COALESCE(s.started_at + a.first_join_seconds,
       (m.payload->>'startedAt')::double precision, extract(epoch FROM h.started_at))) AS observed_at,
       h.server_id, p.complete AS stats_complete, p.finalized AS stats_finalized,
       p.schema_version AS stats_schema_version,
       COALESCE(CASE WHEN p.player_id IS NOT NULL
           THEN p.schema_version >= 3 AND p.complete AND p.finalized
           ELSE h.closed AND h.complete AND NOT EXISTS (
               SELECT 1 FROM lobby2.mission_phases phase
               WHERE phase.mission_id = a.mission_id AND phase.phase = 'GAME')
           END, false) AS playtime_known
FROM lobby2.mission_attendance a
LEFT JOIN anchors s USING (mission_id)
LEFT JOIN lobby2.missions m USING (mission_id)
LEFT JOIN lobby2.mission_history h USING (mission_id)
LEFT JOIN lobby2.mission_players p USING (mission_id, player_id);

CREATE OR REPLACE VIEW lobby2.player_history AS
SELECT player_id, min(observed_at) AS first_seen_at, max(observed_at) AS last_visit_at,
       count(*) AS missions_visited, count(DISTINCT server_id) AS known_servers_visited,
       sum(recorded_joins) AS recorded_joins,
       count(*) FILTER (WHERE connected_during_game) AS missions_played,
       sum(play_seconds) AS recorded_play_seconds,
       count(*) FILTER (WHERE NOT playtime_known) AS unknown_playtime_missions,
       count(*) FILTER (WHERE connected_during_game AND NOT COALESCE(stats_complete, false)) AS partial_stats_missions
FROM lobby2.player_visits GROUP BY player_id;

-- Activity day means a UTC mission-visit day, not continuous presence across midnight.
CREATE OR REPLACE VIEW lobby2.player_population_daily AS
SELECT (v.observed_at AT TIME ZONE 'UTC')::date AS day,
       count(DISTINCT v.player_id) AS unique_players,
       count(DISTINCT v.player_id) FILTER (WHERE (h.first_seen_at AT TIME ZONE 'UTC')::date =
           (v.observed_at AT TIME ZONE 'UTC')::date) AS first_seen_players,
       count(DISTINCT v.player_id) FILTER (WHERE (h.first_seen_at AT TIME ZONE 'UTC')::date <
           (v.observed_at AT TIME ZONE 'UTC')::date) AS returning_players,
       sum(v.recorded_joins) AS recorded_joins, sum(v.play_seconds) AS recorded_play_seconds
FROM lobby2.player_visits v JOIN lobby2.player_history h USING (player_id)
WHERE v.observed_at IS NOT NULL GROUP BY (v.observed_at AT TIME ZONE 'UTC')::date;
