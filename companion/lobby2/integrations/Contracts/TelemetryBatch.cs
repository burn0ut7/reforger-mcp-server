using System.Buffers.Binary;
using System.Text.Json;

public static class TelemetryBatch
{
    public static JsonDocument Read(byte[] bytes)
    {
        if (bytes.Length is 0 or > TelemetryContract.MaxBatchBytes) throw new InvalidDataException("Invalid telemetry size.");
        var binary = bytes.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(bytes) == TelemetryBinary.Magic;
        var document = JsonDocument.Parse(binary ? TelemetryBinary.Decode(bytes) : bytes);
        try { TelemetryContract.Validate(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
}
