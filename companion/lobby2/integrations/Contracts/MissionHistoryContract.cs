using System.Text.Json;

public static class MissionHistoryContract
{
    public static void Validate(JsonElement root)
    {
        Object(root);
        if (Text(root, "kind", 32) != "missionHistory" || Count(root, "schemaVersion") != 1) Fail();
        foreach (var key in new[] { "missionId", "serverId" })
            if (!Guid.TryParse(Text(root, key, 36), out _)) Fail();
        Count(root, "revision");
        var started = Count(root, "startedAt");
        var ended = Count(root, "endedAt");
        if (ended != 0 && ended < started) Fail();
        var duration = Number(root, "observedSeconds");
        foreach (var key in new[] { "serverName", "missionName", "author" }) Text(root, key, 4096);
        foreach (var key in new[] { "world", "scenarioPath" }) Text(root, key, 4096);
        Text(root, "description", 65536);
        if (Text(root, "hostKind", 32) is not ("dedicated" or "listen" or "workbench")) Fail();
        var status = Text(root, "status", 32);
        if (status is not ("pregame" or "running" or "completed" or "interrupted")) Fail();
        var outcome = Text(root, "outcome", 32);
        if (outcome is not ("unknown" or "victory" or "draw" or "failed" or "defeat" or "complete")) Fail();
        root.GetProperty("endReason").GetInt32();
        if (root.TryGetProperty("endTitle", out _)) Text(root, "endTitle", 256);
        if (root.TryGetProperty("endMessage", out _)) Text(root, "endMessage", 512);
        var closed = root.GetProperty("closed").GetBoolean();
        root.GetProperty("complete").GetBoolean();
        if ((closed || status is "completed" or "interrupted") && ended == 0) Fail();
        if (status is "pregame" or "running" && (ended != 0 || outcome != "unknown")) Fail();
        var resultFactions = Strings(root, "resultFactions", 128);
        var winners = Strings(root, "winningFactions", 128);
        if ((outcome == "victory") != (winners.Count > 0) || !winners.IsSubsetOf(resultFactions)) Fail();
        var factionKeys = new HashSet<string>();
        foreach (var faction in Array(root, "factions", 128))
        {
            Object(faction);
            if (!factionKeys.Add(Text(faction, "key", 128))) Fail();
            Text(faction, "name", 4096);
        }
        double previous = -1;
        foreach (var phase in Array(root, "phases", 16))
        {
            Object(phase);
            if (Text(phase, "phase", 32) is not ("PREGAME" or "SLOTTING" or "BRIEFING" or "GAME" or "POSTGAME")) Fail();
            var seconds = Number(phase, "sessionSeconds");
            if (seconds < previous || seconds > duration + 0.1) Fail();
            previous = seconds;
            if (Count(phase, "at") < started) Fail();
        }
        var lastCommandEnd = new Dictionary<string, double>();
        foreach (var commander in Array(root, "commanders", 512))
        {
            Object(commander);
            var faction = Text(commander, "faction", 128);
            Text(commander, "factionName", 4096);
            if (Text(commander, "identity", 256).Length == 0) Fail();
            Text(commander, "name", 1024);
            var from = Number(commander, "fromSeconds");
            var until = Number(commander, "untilSeconds");
            if (until < from || until > duration + 0.1 || from < lastCommandEnd.GetValueOrDefault(faction)) Fail();
            lastCommandEnd[faction] = until;
        }
        Performance(root.GetProperty("sessionPerformance"));
        Performance(root.GetProperty("gamePerformance"));
    }

    private static void Performance(JsonElement value)
    {
        Object(value);
        var samples = Count(value, "samples");
        var mean = Number(value, "meanSampleFps");
        var minimum = Number(value, "minimumSampleFps");
        var maximum = Number(value, "maximumSampleFps");
        if (minimum > mean + 0.01 || mean > maximum + 0.01) Fail();
        if (Count(value, "samplesBelow15Fps") > samples) Fail();
        Count(value, "peakSampledPlayers");
        Count(value, "peakSampledActiveAI");
        Number(value, "largestSampleGapSeconds");
        if (samples == 0 && (mean != 0 || minimum != 0 || maximum != 0)) Fail();
    }

    private static HashSet<string> Strings(JsonElement value, string name, int maximum)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Array(value, name, maximum))
        {
            var text = item.GetString();
            if (string.IsNullOrEmpty(text) || text.Length > 128 || text.Contains('\0') || !values.Add(text)) Fail();
        }
        return values;
    }

    private static void Object(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) Fail();
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement value, string name, int maximum)
    {
        var items = value.GetProperty(name);
        if (items.GetArrayLength() > maximum) Fail();
        return items.EnumerateArray();
    }

    private static string Text(JsonElement value, string name, int maximum)
    {
        var text = value.GetProperty(name).GetString();
        if (text is null || text.Length > maximum || text.Contains('\0')) Fail();
        return text!;
    }

    private static int Count(JsonElement value, string name)
    {
        var number = value.GetProperty(name).GetInt32();
        if (number < 0) Fail();
        return number;
    }

    private static double Number(JsonElement value, string name)
    {
        var number = value.GetProperty(name).GetDouble();
        if (!double.IsFinite(number) || number < 0 || number > float.MaxValue) Fail();
        return number;
    }

    private static void Fail() => throw new InvalidDataException("Invalid mission history batch.");
}
