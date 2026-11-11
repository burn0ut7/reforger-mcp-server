using System.Text.Json;

/// <summary>Canonical routing, limits and validation for server outbox batches.</summary>
public sealed class LobbyBatch : IDisposable
{
    public const int MaximumJsonBytes = 64 * 1024 * 1024;
    public JsonDocument Document { get; }
    public JsonElement Root => Document.RootElement;
    public string Stream { get; }
    public Guid Id { get; }
    public Guid MissionId { get; }
    public int Revision { get; }
    public bool Binary { get; }
    public string FileName => Stream switch
    {
        "telemetry" => $"{Id}-telemetry-{Revision}.{(Binary ? "l2t" : "json")}",
        "session" => $"{Id}-events-{Revision}.json",
        "history" => $"{Id}-history-{Revision}.json",
        _ => $"{Id}-{Revision}.json"
    };

    private LobbyBatch(JsonDocument document, string stream, bool binary)
    {
        Document = document; Stream = stream; Binary = binary;
        Id = Root.GetProperty(stream == "telemetry" ? "recordingId" : stream == "session" ? "sessionId" : "missionId").GetGuid();
        MissionId = stream == "telemetry" ? Root.GetProperty("missionId").GetGuid() : Id;
        Revision = Root.GetProperty("revision").GetInt32();
        if (Id == Guid.Empty || MissionId == Guid.Empty || Revision < 0) throw new InvalidDataException("Invalid batch identity.");
    }

    public static int MaximumBytes(string stream) => stream switch
    {
        "telemetry" => TelemetryContract.MaxBatchBytes,
        "stats" or "session" or "history" => MaximumJsonBytes,
        _ => throw new ArgumentException("Unknown stream.")
    };

    public static LobbyBatch Read(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaximumJsonBytes) throw new InvalidDataException("Invalid batch size.");
        var binary = bytes.AsSpan().StartsWith("L2TB"u8);
        // Preserve strict UTF-8 even in additive fields that validators do not inspect.
        if (!binary) _ = new System.Text.UTF8Encoding(false, true).GetCharCount(bytes);
        var document = binary ? TelemetryBatch.Read(bytes) : JsonDocument.Parse(bytes);
        try
        {
            var root = document.RootElement;
            var kind = root.TryGetProperty("kind", out var value) ? value.GetString() ?? throw new InvalidDataException("Invalid kind.") : null;
            var stream = kind switch { null => "stats", "sessionEvents" => "session", "missionHistory" => "history", "telemetry" => "telemetry", _ => throw new InvalidDataException("Unknown batch kind.") };
            if (bytes.Length > MaximumBytes(stream)) throw new InvalidDataException("Batch exceeds stream size limit.");
            switch (stream)
            {
                case "stats": BatchContract.Validate(root); break;
                case "session": SessionEventContract.Validate(root); break;
                case "history": MissionHistoryContract.Validate(root); break;
                case "telemetry": if (!binary) TelemetryContract.Validate(root); break;
            }
            return new LobbyBatch(document, stream, binary);
        }
        catch { document.Dispose(); throw; }
    }

    public static bool IsInvalid(Exception error) => error is JsonException or InvalidDataException or FormatException
        or InvalidOperationException or KeyNotFoundException or OverflowException or ArgumentException;
    public void Dispose() => Document.Dispose();
}
