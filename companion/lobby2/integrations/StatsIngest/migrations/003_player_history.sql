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
