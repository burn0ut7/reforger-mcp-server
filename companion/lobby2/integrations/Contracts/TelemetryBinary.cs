using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

// L2TB v1. Numeric movement tables are little-endian IEEE binary32/int32.
// Cold events and metadata preserve the existing schema-2 JSON contract.
public static class TelemetryBinary
{
    public const int Magic = 0x4254324C;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly string[] Vectors = ["position", "anglesDegrees", "velocity", "aimRadians", "lookRadians"];
    private static readonly string[] Scalars = ["lean", "dynamicStance", "movement"];

    public static byte[] Encode(JsonElement root)
    {
        TelemetryContract.Validate(root);
        if (root.GetProperty("schemaVersion").GetInt32() != 2) throw new InvalidDataException("Binary requires schema 2.");
        var states = new List<JsonElement>();
        var vehicles = new List<JsonElement>();
        var cold = new List<JsonElement>();
        foreach (var record in root.GetProperty("records").EnumerateArray())
        {
            var type = record.GetProperty("type").GetString();
            (type == "state" ? states : type == "vehicle-state" ? vehicles : cold).Add(record);
        }
        using var headerStream = new MemoryStream();
        using (var json = new Utf8JsonWriter(headerStream))
        {
            json.WriteStartObject();
            foreach (var property in root.EnumerateObject())
                if (property.Name != "records") property.WriteTo(json);
            json.WriteEndObject();
        }
        var header = headerStream.ToArray();
        using var coldStream = new MemoryStream();
        using (var json = new Utf8JsonWriter(coldStream))
        {
            json.WriteStartArray();
            foreach (var record in cold) json.WriteRawValue(record.GetRawText());
            json.WriteEndArray();
        }
        var events = coldStream.ToArray();
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, true);
        foreach (var value in new[] { Magic, 1, header.Length, events.Length, states.Count, vehicles.Count }) writer.Write(value);
        writer.Write(header);
        writer.Write(events);
        WriteTable(writer, states, true);
        WriteTable(writer, vehicles, false);
        if (output.Length > TelemetryContract.MaxBatchBytes) throw new InvalidDataException("Binary batch too large.");
        return output.ToArray();
    }

    private static void WriteTable(BinaryWriter writer, List<JsonElement> records, bool character)
    {
        var allowed = new HashSet<string>(["entity", "position", "anglesDegrees", "velocity", "discontinuity", "previousSampleMs"]);
        if (character) allowed.UnionWith(["aimRadians", "lookRadians", "stance", "command", "flags", "lean", "dynamicStance", "movement"]);
        foreach (var record in records)
        {
            if (record.EnumerateObject().Any(p => p.Name is not ("sequence" or "timeMs" or "type" or "data")))
                throw new InvalidDataException("Unknown record fields cannot be discarded.");
            var data = record.GetProperty("data");
            if (data.EnumerateObject().Any(p => !allowed.Contains(p.Name))) throw new InvalidDataException("Unknown state fields cannot be discarded.");
            var entity = data.GetProperty("entity").GetString()!;
            if (!entity.StartsWith("entity-", StringComparison.Ordinal) || !int.TryParse(entity.AsSpan(7), out var id)
                || id < 1 || entity != $"entity-{id}") throw new InvalidDataException("Noncanonical numeric actor ID.");
            writer.Write(record.GetProperty("sequence").GetInt32());
            writer.Write(id);
            var bits = data.GetProperty("discontinuity").GetBoolean() ? 1 : 0;
            if (character) bits = data.GetProperty("stance").GetInt32() | (data.GetProperty("flags").GetInt32() << 8) | (bits << 16);
            writer.Write(bits);
            if (character) writer.Write(data.GetProperty("command").GetInt32());
        }
        foreach (var record in records)
        {
            var data = record.GetProperty("data");
            WriteFloat(writer, record.GetProperty("timeMs"));
            WriteFloat(writer, data.GetProperty("previousSampleMs"));
            foreach (var name in Vectors.Take(character ? 5 : 3))
                foreach (var value in data.GetProperty(name).EnumerateArray()) WriteFloat(writer, value);
            if (character)
                foreach (var name in Scalars) WriteFloat(writer, data.GetProperty(name));
        }
    }

    private static void WriteFloat(BinaryWriter writer, JsonElement value)
    {
        var number = value.GetSingle();
        if (!float.IsFinite(number)) throw new InvalidDataException("Nonfinite numeric value.");
        writer.Write(number);
    }

    // Decoding reconstructs the existing logical JSON for validation/JSONB ingestion.
    // Browser decoding goes directly to playback objects instead (web module).
    public static byte[] Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || bytes.Length > TelemetryContract.MaxBatchBytes) throw new InvalidDataException("Invalid binary size.");
        if (ReadInt(bytes, 0) != Magic || ReadInt(bytes, 4) != 1) throw new InvalidDataException("Unsupported binary version.");
        var headerLength = ReadInt(bytes, 8);
        var coldLength = ReadInt(bytes, 12);
        var states = ReadInt(bytes, 16);
        var vehicles = ReadInt(bytes, 20);
        if (headerLength < 2 || coldLength < 2 || states < 0 || vehicles < 0 || (long)states + vehicles > TelemetryContract.MaxRecords
            || 24L + headerLength + coldLength + states * 96L + vehicles * 56L != bytes.Length)
            throw new InvalidDataException("Invalid binary lengths/counts.");
        using var header = JsonDocument.Parse(Utf8.GetString(bytes.Slice(24, headerLength)));
        using var cold = JsonDocument.Parse(Utf8.GetString(bytes.Slice(24 + headerLength, coldLength)));
        if (header.RootElement.TryGetProperty("records", out _) || header.RootElement.GetProperty("schemaVersion").GetInt32() != 2)
            throw new InvalidDataException("Invalid binary header.");
        if (cold.RootElement.GetArrayLength() + (long)states + vehicles > TelemetryContract.MaxRecords)
            throw new InvalidDataException("Too many records.");
        var records = new SortedDictionary<int, byte[]>();
        foreach (var record in cold.RootElement.EnumerateArray())
        {
            if (record.GetProperty("type").GetString() is "state" or "vehicle-state") throw new InvalidDataException("State in cold table.");
            AddRecord(records, record.GetProperty("sequence").GetInt32(), Utf8.GetBytes(record.GetRawText()));
        }
        var offset = 24 + headerLength + coldLength;
        ReadTable(bytes.Slice(offset, states * 96), states, true, records);
        offset += states * 96;
        ReadTable(bytes[offset..], vehicles, false, records);
        using var output = new MemoryStream();
        using (var json = new Utf8JsonWriter(output))
        {
            json.WriteStartObject();
            foreach (var property in header.RootElement.EnumerateObject()) property.WriteTo(json);
            json.WriteStartArray("records");
            foreach (var record in records.Values) json.WriteRawValue(record);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        var decoded = output.ToArray();
        using var parsed = JsonDocument.Parse(decoded);
        TelemetryContract.Validate(parsed.RootElement);
        return decoded;
    }

    private static void AddRecord(SortedDictionary<int, byte[]> records, int sequence, byte[] record)
    {
        if (!records.TryAdd(sequence, record)) throw new InvalidDataException("Duplicate binary sequence.");
    }

    private static void ReadTable(ReadOnlySpan<byte> bytes, int count, bool character, SortedDictionary<int, byte[]> records)
    {
        var idStride = character ? 16 : 12;
        var floatStride = character ? 80 : 44;
        for (var i = 0; i < count; i++)
        {
            var ids = bytes.Slice(i * idStride, idStride);
            var values = bytes.Slice(count * idStride + i * floatStride, floatStride);
            var sequence = ReadInt(ids, 0);
            var entity = ReadInt(ids, 4);
            var bits = ReadInt(ids, 8);
            if (entity < 1 || (character ? (bits & ~0x11F03) != 0 || (bits & 255) > 2 : bits is not (0 or 1)))
                throw new InvalidDataException("Invalid binary actor/flags.");
            using var record = new MemoryStream();
            using (var json = new Utf8JsonWriter(record))
            {
                json.WriteStartObject();
                json.WriteNumber("sequence", sequence);
                json.WriteNumber("timeMs", ReadFloat(values, 0, 6));
                json.WriteString("type", character ? "state" : "vehicle-state");
                json.WriteStartObject("data");
                json.WriteString("entity", $"entity-{entity}");
                json.WriteNumber("previousSampleMs", ReadFloat(values, 4, 6));
                var cursor = 8;
                foreach (var name in Vectors.Take(character ? 5 : 3))
                {
                    json.WriteStartArray(name);
                    for (var axis = 0; axis < 3; axis++, cursor += 4) json.WriteNumberValue(ReadFloat(values, cursor, 3));
                    json.WriteEndArray();
                }
                json.WriteBoolean("discontinuity", character ? (bits & 65536) != 0 : bits != 0);
                if (character)
                {
                    json.WriteNumber("stance", bits & 255);
                    json.WriteNumber("flags", (bits >> 8) & 31);
                    json.WriteNumber("command", ReadInt(ids, 12));
                    foreach (var name in Scalars)
                    {
                        json.WriteNumber(name, ReadFloat(values, cursor, 3));
                        cursor += 4;
                    }
                }
                json.WriteEndObject();
                json.WriteEndObject();
            }
            AddRecord(records, sequence, record.ToArray());
        }
    }

    private static int ReadInt(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
    private static double ReadFloat(ReadOnlySpan<byte> bytes, int offset, int decimals)
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
        if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite binary value.");
        // Native JSON clocks truncate at six decimals; retain their batch boundaries.
        return decimals == 6 ? Math.Truncate((double)value * 1e6) / 1e6 : Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
