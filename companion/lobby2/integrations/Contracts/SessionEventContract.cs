using System.Text.Json;

public static class SessionEventContract
{
    public static void Validate(JsonElement root)
    {
        UniqueFields(root);
        if (root.GetProperty("kind").GetString() != "sessionEvents"
            || root.GetProperty("schemaVersion").GetInt32() != 1) Fail();
        root.GetProperty("sessionId").GetGuid();
        Text(root, "world", 4096);
        Nonnegative(root, "revision");
        Nonnegative(root, "startedAt");
        Nonnegative(root, "droppedRecords");
        root.GetProperty("closed").GetBoolean();
        var events = root.GetProperty("events");
        if (events.GetArrayLength() > 256) Fail();
        var previous = 0;
        double previousTime = 0;
        foreach (var entry in events.EnumerateArray())
        {
            UniqueFields(entry);
            var sequence = Nonnegative(entry, "sequence");
            var time = Number(entry, "sessionSeconds", 0);
            if (sequence <= previous || time < previousTime) Fail();
            previous = sequence;
            previousTime = time;
            if (Text(entry, "type", 32) is not ("shot" or "grenade" or "damage" or "kill" or "aiKill" or "teamKill"
                or "death" or "admin" or "joined" or "left" or "session" or "slot")) Fail();
            foreach (var name in new[] { "actor", "target" }) Text(entry, name, 256);
            foreach (var name in new[] { "actorName", "targetName" }) Text(entry, name, 1024);
            foreach (var name in new[] { "weapon", "projectile", "magazine", "detail" }) Text(entry, name, 4096);
            Number(entry, "reportedDamage", -float.MaxValue);
            Number(entry, "distanceMetres", -1);
            if (entry.GetProperty("damageType").GetInt32() < -1) Fail();
            ValidateDeathCredit(entry);
            foreach (var name in new[] { "actorEntity", "targetEntity" })
                if (entry.TryGetProperty(name, out _)) Text(entry, name, 256);
            foreach (var name in new[] { "actorPosition", "targetPosition" })
            {
                if (!entry.TryGetProperty(name, out var position)) continue;
                if (position.GetArrayLength() is not (0 or 3)) Fail();
                foreach (var coordinate in position.EnumerateArray())
                    if (!double.IsFinite(coordinate.GetDouble()) || Math.Abs(coordinate.GetDouble()) > float.MaxValue) Fail();
            }
        }
    }

    // Additive fields: older archived batches remain readable without inventing a cause.
    internal static void ValidateDeathCredit(JsonElement entry)
    {
        foreach (var name in new[] { "actorKind", "targetKind", "victimKind", "killerKind" })
            if (entry.TryGetProperty(name, out _) && Text(entry, name, 16) is not ("" or "player" or "ai" or "other")) Fail();
        foreach (var name in new[] { "actorPrefab", "targetPrefab", "victimPrefab", "killerPrefab" })
            if (entry.TryGetProperty(name, out _)) Text(entry, name, 4096);
        if (entry.TryGetProperty("deathCause", out _) && Text(entry, "deathCause", 32) is not ("" or "other" or "bleeding" or "respawn" or "suicide")) Fail();
        if (entry.TryGetProperty("creditSource", out _) && Text(entry, "creditSource", 32) is not ("" or "engine" or "lastPlayerDamage" or "lastDamage")) Fail();
        if (entry.TryGetProperty("damageEventSequence", out _)) Nonnegative(entry, "damageEventSequence");
    }

    private static void UniqueFields(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) Fail();
    }

    private static int Nonnegative(JsonElement value, string name)
    {
        var result = value.GetProperty(name).GetInt32();
        if (result < 0) Fail();
        return result;
    }

    private static double Number(JsonElement value, string name, double minimum)
    {
        var result = value.GetProperty(name).GetDouble();
        if (!double.IsFinite(result) || result < minimum || result > float.MaxValue) Fail();
        return result;
    }

    private static string Text(JsonElement value, string name, int maximum)
    {
        var result = value.GetProperty(name).GetString();
        if (result is null || result.Length > maximum || result.Contains('\0')) Fail();
        return result!;
    }

    private static void Fail() => throw new InvalidDataException("Invalid session event batch.");
}
