using System.Text.Json;
using Npgsql;

internal static class TelemetryIngest
{
    // Out-of-order batches are allowed. Indexed boundary reads reject conflicting
    // clocks, sequence ranges and records after closure. Serializable ingestion
    // prevents concurrent directories from validating against incompatible views.
    public static async Task ValidateContinuity(NpgsqlConnection connection, NpgsqlTransaction transaction, JsonElement incoming)
    {
        var recording = incoming.GetProperty("recordingId").GetGuid();
        var revision = incoming.GetProperty("revision").GetInt32();
        await using var query = new NpgsqlCommand("""
            SELECT payload FROM lobby2.telemetry_batches
            WHERE recording_id=$1 AND revision IN (
                (SELECT max(revision) FROM lobby2.telemetry_batches WHERE recording_id=$1 AND revision < $2),
                (SELECT min(revision) FROM lobby2.telemetry_batches WHERE recording_id=$1 AND revision > $2),
                $2)
            """, connection, transaction);
        query.Parameters.AddWithValue(recording);
        query.Parameters.AddWithValue((long)revision);
        await using var reader = await query.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            using var stored = JsonDocument.Parse(reader.GetString(0));
            var other = stored.RootElement;
            TelemetryContinuity.Validate(incoming, incoming.GetProperty("records").GetArrayLength(),
                other, other.GetProperty("records").GetArrayLength());
        }
    }
}
