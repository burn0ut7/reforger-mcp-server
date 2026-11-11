using System.Text.Json;

public static class TelemetryContinuity
{
    // Both envelopes must already have passed TelemetryContract.Validate.
    // Caller must group by recording ID and supply each validated records count.
    // Separate counts allow indexes to retain metadata without duplicating records.
    public static void Validate(JsonElement incoming, int incomingCount, JsonElement other, int otherCount)
    {
        foreach (var name in new[] { "schemaVersion", "missionId", "startedAt", "world", "gameVersion", "assetManifestId", "clock", "coordinates", "sampleHz", "coverage" })
            if (!JsonElement.DeepEquals(incoming.GetProperty(name), other.GetProperty(name)))
                throw new InvalidDataException("Telemetry recording metadata changed.");
        var revision = incoming.GetProperty("revision").GetInt32();
        var otherRevision = other.GetProperty("revision").GetInt32();
        if (otherRevision == revision) return; // Immutable-byte checksum belongs to ingestion.
        var earlier = otherRevision < revision ? other : incoming;
        var later = otherRevision < revision ? incoming : other;
        var earlierCount = otherRevision < revision ? otherCount : incomingCount;
        if (earlier.GetProperty("closed").GetBoolean()
            || earlier.GetProperty("untilMs").GetDouble() > later.GetProperty("fromMs").GetDouble()
            || earlier.GetProperty("droppedRecords").GetInt32() > later.GetProperty("droppedRecords").GetInt32())
            throw new InvalidDataException("Telemetry timeline or closure conflicts.");
        var nextSequence = (long)earlier.GetProperty("firstSequence").GetInt32() + earlierCount;
        var laterSequence = later.GetProperty("firstSequence").GetInt32();
        if (nextSequence > laterSequence || (Math.Abs((long)otherRevision - revision) == 1 && nextSequence != laterSequence))
            throw new InvalidDataException("Telemetry sequence range conflicts.");
    }
}
