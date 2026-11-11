-- Preserve all archived v1 recordings; accept compact v2 in the same table.
BEGIN;
DO $$
DECLARE existing record;
BEGIN
    FOR existing IN
        SELECT conname FROM pg_constraint
        WHERE conrelid = 'lobby2.telemetry_batches'::regclass
          AND contype = 'c'
          AND pg_get_constraintdef(oid) LIKE '%schemaVersion%'
    LOOP
        EXECUTE format('ALTER TABLE lobby2.telemetry_batches DROP CONSTRAINT %I', existing.conname);
    END LOOP;
END $$;
ALTER TABLE lobby2.telemetry_batches ADD CONSTRAINT telemetry_schema_version
    CHECK ((payload->>'schemaVersion')::integer IN (1, 2));
COMMIT;
