using System.Text.Json;

public static class BatchContract
{
    // Validate every field consumed by SQL views before acknowledging a local batch.
    public static void Validate(JsonElement root)
    {
        Object(root);
        var version = Integer(root, "schemaVersion", 1, 3);
        if (!Guid.TryParse(Text(root, "missionId", 36), out _)) Fail();
        Text(root, "world", 4096);
        Integer(root, "startedAt", 0);
        Integer(root, "revision", 0);
        root.GetProperty("final").GetBoolean();
        root.GetProperty("complete").GetBoolean();
        var duration = Number(root, "durationSeconds", 0);
        if (version < 3) Integer(root, "minimumPlayersForPlaytime", 5, 5);
        else
        {
            if (Text(root, "playtimeBasis", 32) != "connectedDuringGame") Fail();
            double previous = -1;
            foreach (var point in Array(root, "population", 4096))
            {
                Object(point);
                var from = Number(point, "fromSeconds", 0);
                if ((previous < 0 && from != 0) || from < previous || from > duration + 0.01) Fail();
                Integer(point, "players", 0);
                previous = from;
            }
            if (previous < 0) Fail();
        }
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in Array(root, "players", 1024))
        {
            Object(player);
            var identity = Text(player, "identity", 256);
            if (identity.Length == 0 || !identities.Add(identity)) Fail();
            Text(player, "name", 1024);
            Counters(player, version);
            double previousEnd = 0;
            double recordedSeconds = 0;
            foreach (var assignment in Array(player, "assignments", 256))
            {
                Object(assignment);
                Counters(assignment, version);
                Text(assignment, "faction", 128);
                Text(assignment, "groupId", 256);
                Text(assignment, "groupName", 1024);
                Text(assignment, "leader", 256);
                Text(assignment, "role", 4096);
                Integer(assignment, "slotId", -1);
                var from = Number(assignment, "fromSeconds", 0);
                var until = Number(assignment, "untilSeconds", 0);
                if (until < from || from < previousEnd) Fail();
                if (version >= 3 && (until > duration + 0.01
                    || Math.Abs(Number(assignment, "playSeconds", 0) - (until - from)) > 0.1)) Fail();
                recordedSeconds += until - from;
                previousEnd = until;
                var ammunition = new HashSet<(string, string, string)>();
                foreach (var ammo in Array(assignment, "ammo", 128))
                {
                    Object(ammo);
                    var projectile = Text(ammo, "projectile", 4096);
                    var magazine = Text(ammo, "magazine", 4096);
                    var weapon = Text(ammo, "weapon", 4096);
                    if (!ammunition.Add((projectile, magazine, weapon))) Fail();
                    Integer(ammo, "rounds", 1);
                }
            }
            if (version >= 3 && Math.Abs(Number(player, "playSeconds", 0) - recordedSeconds) > 0.1) Fail();
            var roles = new HashSet<(int, string)>();
            foreach (var role in Array(player, "roles", 4096))
            {
                Object(role);
                var prefab = Text(role, "prefab", 4096);
                Text(role, "name", 1024);
                var slot = Integer(role, "slotId", 0);
                if (!roles.Add((slot, prefab))) Fail();
                Integer(role, "selections", 0);
                Number(role, "seconds", 0);
            }
        }
        var sequences = new HashSet<int>();
        foreach (var death in Array(root, "deaths", 256))
        {
            Object(death);
            if (!sequences.Add(Integer(death, "sequence", 1))) Fail();
            Number(death, "missionSeconds", 0);
            foreach (var name in new[] { "victim", "killer" }) Text(death, name, 256);
            foreach (var name in new[] { "victimName", "killerName" }) Text(death, name, 1024);
            Text(death, "weapon", 4096);
            Integer(death, "relation", 0);
            Number(death, "distanceMetres", -1);
            SessionEventContract.ValidateDeathCredit(death);
        }
    }

    private static void Object(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) Fail();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) Fail();
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement value, string name, int maximum)
    {
        var array = value.GetProperty(name);
        if (array.GetArrayLength() > maximum) Fail();
        return array.EnumerateArray();
    }

    private static string Text(JsonElement value, string name, int maximum)
    {
        var text = value.GetProperty(name).GetString();
        if (text is null || text.Length > maximum || text.Contains('\0')) Fail();
        return text!;
    }

    private static int Integer(JsonElement value, string name, int minimum, int maximum = int.MaxValue)
    {
        var number = value.GetProperty(name).GetInt32();
        if (number < minimum || number > maximum) Fail();
        return number;
    }

    private static double Number(JsonElement value, string name, double minimum)
    {
        var number = value.GetProperty(name).GetDouble();
        if (!double.IsFinite(number) || number < minimum || number > float.MaxValue) Fail();
        return number;
    }

    private static void Counters(JsonElement value, int version)
    {
        foreach (var name in new[] { "playSeconds", "walkMetres", "driveMetres", "pilotMetres", "radioSecondsClientReported" })
            Number(value, name, 0);
        foreach (var name in new[] { "shots", "grenades", "bandagesOthers", "kills", "deaths" })
            Integer(value, name, 0);
        Integer(value, "commanderMissions", 0, 1);
        Integer(value, "leaderMissions", 0, 1);
        if ((version >= 2 || value.TryGetProperty("teamKills", out _))
            && Integer(value, "teamKills", 0) > Integer(value, "kills", 0)) Fail();
        if (value.TryGetProperty("aiKills", out _) || value.TryGetProperty("deathsByPlayers", out _)
            || value.TryGetProperty("deathsByAI", out _))
        {
            Integer(value, "aiKills", 0);
            var byPlayers = Integer(value, "deathsByPlayers", 0);
            var byAI = Integer(value, "deathsByAI", 0);
            if ((long)byPlayers + byAI > Integer(value, "deaths", 0)) Fail();
        }
    }

    private static void Fail() => throw new InvalidDataException("Invalid stats batch contract.");
}
