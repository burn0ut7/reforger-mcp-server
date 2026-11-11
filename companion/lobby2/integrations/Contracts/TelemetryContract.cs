using System.Text.Json;

// Replay records are separate from aggregate statistics. No engine assets or
// animation graph behavior are inferred by the importer.
public static class TelemetryContract
{
    public const int MaxBatchBytes = 1024 * 1024;
    public const int MaxRecords = 1024;

    public static void Validate(JsonElement root)
    {
        UniqueFields(root);
        var version = Integer(root, "schemaVersion", 1, 2);
        if (Text(root, "kind", 32) != "telemetry") Fail();
        root.GetProperty("missionId").GetGuid();
        root.GetProperty("recordingId").GetGuid();
        var revision = Integer(root, "revision", 0);
        Integer(root, "startedAt", 0);
        Text(root, "world", 4096, false);
        Text(root, "gameVersion", 128, false);
        Text(root, "assetManifestId", 256, false);
        if (Text(root, "clock", 32) != "session-world-ms") Fail();
        if (Text(root, "coordinates", 64) != "enfusion-world-metres") Fail();
        Number(root, "sampleHz", 1, 30);
        var firstSequence = Integer(root, "firstSequence", 1);
        if (revision == 0 && firstSequence != 1) Fail();
        var from = Number(root, "fromMs", 0);
        var until = Number(root, "untilMs", from);
        var closed = root.GetProperty("closed").GetBoolean();
        var reason = Text(root, "stopReason", 32);
        if ((!closed && reason != "") || (closed && reason is not ("disabled" or "mission-end" or "overflow" or "storage-error"))) Fail();
        Integer(root, "droppedRecords", 0);
        var coverage = root.GetProperty("coverage");
        UniqueFields(coverage);
        if (Text(coverage, "actors", 32) is not ("players" or "players-and-ai")) Fail();
        var pose = Text(coverage, "pose", 32);
        if (version == 2 ? pose != "inferred" : pose is not ("state" or "skeletal")) Fail();
        foreach (var name in new[] { "shots", "impacts", "damage", "grenades", "vehicles", "gear" })
            coverage.GetProperty(name).GetBoolean();

        var records = root.GetProperty("records");
        if (records.GetArrayLength() > MaxRecords || (records.GetArrayLength() == 0 && !closed)) Fail();
        if ((long)firstSequence + records.GetArrayLength() > int.MaxValue) Fail();
        double previousTime = from;
        long expectedSequence = firstSequence;
        foreach (var record in records.EnumerateArray())
        {
            UniqueFields(record);
            if (Integer(record, "sequence", 1) != expectedSequence++) Fail();
            previousTime = Number(record, "timeMs", previousTime, until);
            var type = Text(record, "type", 32);
            var data = record.GetProperty("data");
            UniqueFields(data);
            ValidateRecord(type, data, version);
            if (version == 2 && type is "state" or "vehicle-state")
                Number(data, "previousSampleMs", 0, previousTime);
        }
    }

    private static void ValidateRecord(string type, JsonElement data, int version)
    {
        switch (type)
        {
            case "turret-state":
                if (version != 2) Fail();
                Entity(data);
                var available = data.GetProperty("available").GetBoolean();
                var turrets = data.GetProperty("turrets");
                if (turrets.GetArrayLength() > 64 || (!available && turrets.GetArrayLength() != 0)) Fail();
                var slots = new HashSet<(int, int)>();
                foreach (var turret in turrets.EnumerateArray())
                {
                    UniqueFields(turret);
                    var managerId = Integer(turret, "managerId", int.MinValue);
                    var slotId = Integer(turret, "slotId", 0);
                    if (!slots.Add((managerId, slotId))) Fail();
                    Integer(turret, "seatIndex", 0);
                    Integer(turret, "compartmentType", 0);
                    turret.GetProperty("hasWeapon").GetBoolean();
                    if (turret.GetProperty("directionAvailable").GetBoolean())
                    {
                        Vector(turret, "direction");
                        var lengthSquared = turret.GetProperty("direction").EnumerateArray().Sum(v => v.GetDouble() * v.GetDouble());
                        if (Math.Abs(lengthSquared - 1) > 0.001) Fail();
                    }
                    else if (turret.TryGetProperty("direction", out _)) Fail();
                }
                break;
            case "voice-start":
            case "voice-refresh":
            case "voice-stop":
                Entity(data);
                if (Text(data, "channel", 16) is not ("local" or "radio")) Fail();
                if (Text(data, "source", 32) != "client-capture") Fail();
                Integer(data, "validForMs", type == "voice-stop" ? 0 : 1500, type == "voice-stop" ? 0 : 1500);
                break;
            case "map-state":
                Text(data, "faction", 256);
                data.GetProperty("markersAvailable").GetBoolean();
                data.GetProperty("drawingsAvailable").GetBoolean();
                var shapes = data.GetProperty("shapes");
                if (shapes.GetArrayLength() > 384) Fail();
                foreach (var shape in shapes.EnumerateArray())
                {
                    UniqueFields(shape);
                    var points = shape.GetProperty("points");
                    if (points.GetArrayLength() < 4 || points.GetArrayLength() > 512 || points.GetArrayLength() % 2 != 0) Fail();
                    foreach (var point in points.EnumerateArray())
                        if (!point.TryGetDouble(out var coordinate) || !double.IsFinite(coordinate) || Math.Abs(coordinate) > 1000000) Fail();
                    shape.GetProperty("lineColor").GetInt32();
                    shape.GetProperty("fillColor").GetInt32();
                    Number(shape, "width", 0, 256);
                    shape.GetProperty("inverted").GetBoolean();
                    shape.GetProperty("closed").GetBoolean();
                    shape.GetProperty("worldWidth").GetBoolean();
                }
                var markers = data.GetProperty("markers");
                if (markers.GetArrayLength() > 512) Fail();
                foreach (var marker in markers.EnumerateArray())
                {
                    UniqueFields(marker);
                    Vector(marker, "position");
                    Text(marker, "label", 4096);
                    Text(marker, "iconResource", 4096);
                    Text(marker, "iconName", 256);
                    marker.GetProperty("color").GetInt32();
                    Number(marker, "rotation", -360000, 360000);
                    Number(marker, "size", 0, 1000000);
                    Number(marker, "minSize", 0, 1000000);
                    marker.GetProperty("worldScale").GetBoolean();
                    Integer(marker, "nativeType", -1);
                    Integer(marker, "nativeConfig", -1);
                    if (marker.TryGetProperty("nativeFlags", out _)) Integer(marker, "nativeFlags", int.MinValue);
                    if (marker.TryGetProperty("militaryIdentity", out _)) Integer(marker, "militaryIdentity", -1);
                    if (marker.TryGetProperty("militaryDimension", out _)) Integer(marker, "militaryDimension", -1);
                }
                break;
            case "spawn":
                Entity(data);
                if (Text(data, "kind", 16) is not ("player" or "ai" or "vehicle")) Fail();
                Text(data, "prefab", 4096, false);
                if (data.TryGetProperty("iconResource", out _)) Text(data, "iconResource", 4096);
                if (data.TryGetProperty("iconName", out _)) Text(data, "iconName", 256);
                Text(data, "playerIdentity", 256);
                Text(data, "faction", 256);
                RootTransform(data, version);
                break;
            case "despawn":
                Entity(data);
                Text(data, "reason", 128, false);
                break;
            case "ownership":
                Entity(data);
                Text(data, "playerIdentity", 256);
                break;
            case "appearance":
                Entity(data);
                Text(data, "faction", 256);
                Integer(data, "revision", 0);
                Text(data, "visualResource", 4096, false);
                // Opaque native gear tree, not an invented web model format.
                if (Text(data, "loadoutFormat", 64) != "SCR_PlayerArsenalLoadout") Fail();
                Text(data, "loadout", 65536, false);
                break;
            case "state":
                Entity(data);
                RootTransform(data, version);
                Vector(data, "velocity");
                if (version == 1) Number(data, "speedMps", 0, 2000);
                else if (data.TryGetProperty("speedMps", out _)) Fail();
                Vector(data, "aimRadians");
                Vector(data, "lookRadians");
                Integer(data, "stance", 0, 2);
                Integer(data, "command", 0);
                Integer(data, "flags", 0, 31);
                Number(data, "lean", -1, 1);
                Number(data, "dynamicStance", 0, 1);
                Number(data, "movement", 0, 3);
                if (version == 1) Equipment(data);
                else if (data.TryGetProperty("weaponPrefab", out _) || data.TryGetProperty("weaponIndex", out _) || data.TryGetProperty("sightsIndex", out _)) Fail();
                data.GetProperty("discontinuity").GetBoolean();
                break;
            case "vehicle-state":
                Entity(data);
                RootTransform(data, version);
                Vector(data, "velocity");
                if (version == 1) Number(data, "speedMps", 0, 2000);
                else if (data.TryGetProperty("speedMps", out _)) Fail();
                data.GetProperty("discontinuity").GetBoolean();
                break;
            case "seat":
                Entity(data);
                var vehicle = Text(data, "vehicle", 128);
                var seat = Integer(data, "seatIndex", -1);
                var compartment = Integer(data, "compartmentType", -1);
                if ((vehicle == "") != (seat == -1 && compartment == -1) || (vehicle != "" && (seat < 0 || compartment < 0))) Fail();
                break;
            case "equipment":
                if (version != 2) Fail();
                Entity(data);
                Equipment(data);
                break;
            case "skeleton":
                if (version != 1) Fail();
                Text(data, "skeletonId", 128, false);
                Text(data, "visualResource", 4096, false);
                var bones = data.GetProperty("bones");
                if (bones.GetArrayLength() is < 1 or > 512) Fail();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var bone in bones.EnumerateArray())
                {
                    var name = bone.GetString();
                    if (string.IsNullOrEmpty(name) || name.Length > 256 || name.Contains('\0') || !names.Add(name)) Fail();
                }
                break;
            case "pose":
                if (version != 1) Fail();
                Entity(data);
                Text(data, "skeletonId", 128, false);
                // Same order as the skeleton record; native local matrices,
                // not animation names or presumed normalized quaternions.
                var matrices = data.GetProperty("localMatrices");
                if (matrices.GetArrayLength() is < 1 or > 512) Fail();
                foreach (var matrix in matrices.EnumerateArray()) Values(matrix, 12);
                if (data.TryGetProperty("weaponRelativeTransform", out var weaponTransform)) Values(weaponTransform, 12);
                break;
            case "shot":
            case "grenade":
                Entity(data);
                Text(data, "projectileId", 128, false);
                Text(data, "weaponPrefab", 4096);
                Text(data, "projectilePrefab", 4096, false);
                Vector(data, "origin");
                Vector(data, "velocity");
                Integer(data, "sessionEventSequence", 0);
                break;
            case "projectile-sample":
                Text(data, "projectileId", 128, false);
                Vector(data, "position");
                Vector(data, "velocity");
                break;
            case "impact":
            case "detonation":
                Text(data, "projectileId", 128, false);
                Text(data, "target", 128);
                Vector(data, "position");
                Vector(data, "normal");
                // A last observed position is not a verified hit.
                if (Text(data, "evidence", 32) != "engine-event") Fail();
                break;
            case "damage":
                Entity(data);
                Text(data, "attacker", 128);
                Text(data, "projectileId", 128);
                Number(data, "reportedDamage", -float.MaxValue);
                if (data.TryGetProperty("healthRemoved", out _)) Number(data, "healthRemoved", 0);
                Integer(data, "damageType", 0);
                Vector(data, "position");
                Integer(data, "sessionEventSequence", 0);
                break;
            case "death":
                Entity(data);
                if (version == 2) RootTransform(data, version);
                Text(data, "killer", 128);
                Integer(data, "damageSequence", 0);
                Integer(data, "sessionEventSequence", 0);
                Text(data, "cause", 128, false);
                break;
            case "gap":
                var from = Number(data, "fromMs", 0);
                Number(data, "untilMs", from);
                Text(data, "track", 32, false);
                Text(data, "reason", 128, false);
                Integer(data, "droppedRecords", 1);
                break;
            default:
                Fail();
                break;
        }
    }

    private static void RootTransform(JsonElement data, int version)
    {
        if (version == 1) Matrix(data, "transform");
        else
        {
            if (data.TryGetProperty("transform", out _)) Fail();
            Vector(data, "position");
            Vector(data, "anglesDegrees");
        }
    }

    private static void Equipment(JsonElement data)
    {
        Text(data, "weaponPrefab", 4096);
        Integer(data, "weaponIndex", -1);
        Integer(data, "sightsIndex", -1);
    }

    private static void Entity(JsonElement value) => Text(value, "entity", 128, false);
    private static void Matrix(JsonElement value, string name) => Values(value.GetProperty(name), 12);
    private static void Vector(JsonElement value, string name) => Values(value.GetProperty(name), 3);

    private static void Values(JsonElement value, int count)
    {
        if (value.GetArrayLength() != count) Fail();
        foreach (var number in value.EnumerateArray())
            if (!double.IsFinite(number.GetDouble()) || Math.Abs(number.GetDouble()) > float.MaxValue) Fail();
    }

    private static void UniqueFields(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) Fail();
    }

    private static int Integer(JsonElement value, string name, int min, int max = int.MaxValue)
    {
        var result = value.GetProperty(name).GetInt32();
        if (result < min || result > max) Fail();
        return result;
    }

    private static double Number(JsonElement value, string name, double min, double max = float.MaxValue)
    {
        var result = value.GetProperty(name).GetDouble();
        if (!double.IsFinite(result) || result < min || result > max) Fail();
        return result;
    }

    private static string Text(JsonElement value, string name, int max, bool empty = true)
    {
        var result = value.GetProperty(name).GetString();
        if (result is null || result.Length > max || result.Contains('\0') || (!empty && result.Length == 0)) Fail();
        return result!;
    }

    private static void Fail() => throw new InvalidDataException("Invalid telemetry batch.");
}

