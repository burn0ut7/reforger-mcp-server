using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

if (args.Length == 2 && args[0] == "--serve-profile")
{
    var profile = Path.GetFullPath(args[1]);
    if (!Directory.Exists(profile)) throw new DirectoryNotFoundException("Profile not found.");
    var connection = Environment.GetEnvironmentVariable("LOBBY2_STATS_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set LOBBY2_STATS_CONNECTION_STRING.");
    using var owner = new FileStream(Path.Combine(profile, ".player-history.lock"), FileMode.OpenOrCreate,
        FileAccess.ReadWrite, FileShare.None);
    await using var database = NpgsqlDataSource.Create(connection);
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
    while (!stopping.IsCancellationRequested)
    {
        var imported = true;
        foreach (var name in new[] { "Lobby2Stats", "Lobby2SessionLogs", "Lobby2History", "Lobby2Telemetry" })
        {
            var outbox = Path.Combine(profile, name);
            if (Directory.Exists(outbox) && await Ingest(new[] { "--once", outbox }) != 0 && name != "Lobby2Telemetry")
                imported = false;
        }
        // Unimported local records may belong to a previous session. Never call missing history zero.
        if (imported) await PlayerHistoryBridge.Process(profile, database);
        try { await Task.Delay(2000, stopping.Token); }
        catch (OperationCanceledException) { break; }
    }
    return 0;
}
return await Ingest(args);

static async Task<int> Ingest(string[] args)
{
    if (args.Length == 3 && args[0] == "--binary-benchmark")
        return TelemetryBinaryTool.Run(args[1], args[2]);
    if (args.Length == 3 && args[0] == "--decode-telemetry")
    {
        if (new FileInfo(args[1]).Length > TelemetryContract.MaxBatchBytes)
            throw new InvalidDataException("Telemetry batch exceeds 1 MiB.");
        var decoded = TelemetryBinary.Decode(File.ReadAllBytes(args[1]));
        using var destination = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write);
        destination.Write(decoded);
        return 0;
    }

    if (args.Length != 2 || args[0] is not ("--once" or "--validate"))
    {
        Console.Error.WriteLine("Usage: StatsIngest --once|--validate <outbox directory> OR --serve-profile <server profile>");
        return 2;
    }

    var directory = Path.GetFullPath(args[1]);
    if (!Directory.Exists(directory))
    {
        Console.Error.WriteLine("Outbox directory does not exist.");
        return 2;
    }

    var connectionString = Environment.GetEnvironmentVariable("LOBBY2_STATS_CONNECTION_STRING");
    if (args[0] == "--once" && string.IsNullOrWhiteSpace(connectionString))
    {
        Console.Error.WriteLine("Set LOBBY2_STATS_CONNECTION_STRING for the PostgreSQL ingestion account.");
        return 2;
    }

    // Exclusive sidecar ownership prevents two local uploaders deleting each other's pending files.
    using var ownership = new FileStream(Path.Combine(directory, ".ingest.lock"), FileMode.OpenOrCreate,
        FileAccess.ReadWrite, FileShare.None);
    await using var database = args[0] == "--once" ? NpgsqlDataSource.Create(connectionString!) : null;
    var failures = 0;
    foreach (var marker in Directory.EnumerateFiles(directory, "*.ready").Where(p => p.EndsWith(".json.ready", StringComparison.Ordinal) || p.EndsWith(".l2t.ready", StringComparison.Ordinal)))
    {
        var path = marker[..^".ready".Length];
        try
        {
            if (await File.ReadAllTextAsync(marker) != "ready")
                throw new InvalidDataException("Incomplete ready marker.");
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 64 * 1024 * 1024)
                throw new InvalidDataException("Batch exceeds the 64 MiB bound or is empty.");
            var bytes = await File.ReadAllBytesAsync(path);
            using var batch = LobbyBatch.Read(bytes);
            var root = batch.Root;
            var payload = root.GetRawText();
            var sessionEvents = batch.Stream == "session";
            var history = batch.Stream == "history";
            var telemetry = batch.Stream == "telemetry";
            var mission = batch.Id;
            var revision = batch.Revision;
            if (Path.GetFileName(path) != batch.FileName)
                throw new InvalidDataException("Batch identity differs from its filename.");
            var checksum = Convert.ToHexString(SHA256.HashData(bytes));
            if (database is null)
            {
                Console.WriteLine($"VALID {mission} revision={revision}");
                continue;
            }

            await using var connection = await database.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync(telemetry
                ? System.Data.IsolationLevel.Serializable : System.Data.IsolationLevel.ReadCommitted);
            if (telemetry)
                await TelemetryIngest.ValidateContinuity(connection, transaction, root);
            // Table/column names come only from this fixed contract choice, never from a payload.
            var table = telemetry ? "telemetry_batches" : history ? "mission_history_batches" : sessionEvents ? "session_log_batches" : "mission_batches";
            var idColumn = telemetry ? "recording_id" : sessionEvents ? "session_id" : "mission_id";
            await using (var insert = new NpgsqlCommand($"""
                INSERT INTO lobby2.{table}({idColumn}, revision, checksum, payload)
                VALUES ($1, $2, $3, $4) ON CONFLICT ({idColumn}, revision) DO NOTHING
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(mission);
                insert.Parameters.AddWithValue(revision);
                insert.Parameters.AddWithValue(checksum);
                insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb, payload);
                await insert.ExecuteNonQueryAsync();
            }
            await using (var check = new NpgsqlCommand(
                $"SELECT checksum FROM lobby2.{table} WHERE {idColumn}=$1 AND revision=$2", connection, transaction))
            {
                check.Parameters.AddWithValue(mission);
                check.Parameters.AddWithValue(revision);
                var existing = (string?)await check.ExecuteScalarAsync();
                if (!string.Equals(existing, checksum, StringComparison.Ordinal))
                    throw new InvalidDataException("Conflicting payload for an existing batch identity; local batch retained.");
            }
            if (sessionEvents)
            {
                foreach (var entry in root.GetProperty("events").EnumerateArray())
                {
                    var sequence = entry.GetProperty("sequence").GetInt32();
                    await using var insertEvent = new NpgsqlCommand("""
                        INSERT INTO lobby2.session_events(session_id, sequence, detail)
                        VALUES ($1, $2, $3) ON CONFLICT (session_id, sequence) DO NOTHING
                        """, connection, transaction);
                    insertEvent.Parameters.AddWithValue(mission);
                    insertEvent.Parameters.AddWithValue(sequence);
                    insertEvent.Parameters.AddWithValue(NpgsqlDbType.Jsonb, entry.GetRawText());
                    await insertEvent.ExecuteNonQueryAsync();
                    await using var checkEvent = new NpgsqlCommand("""
                        SELECT detail = $3 FROM lobby2.session_events WHERE session_id=$1 AND sequence=$2
                        """, connection, transaction);
                    checkEvent.Parameters.AddWithValue(mission);
                    checkEvent.Parameters.AddWithValue(sequence);
                    checkEvent.Parameters.AddWithValue(NpgsqlDbType.Jsonb, entry.GetRawText());
                    if (await checkEvent.ExecuteScalarAsync() is not true)
                        throw new InvalidDataException("Conflicting session event; local batch retained.");
                }
            }
            await transaction.CommitAsync();
            // A crash before removal simply replays the identical idempotent insert next run.
            File.Delete(marker);
            // Session audit JSON remains permanently on the server after successful ingestion.
            if (!sessionEvents && !history && !telemetry) File.Delete(path);
            Console.WriteLine($"STORED {mission} revision={revision}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or InvalidOperationException
            or FormatException or KeyNotFoundException or NpgsqlException or DecoderFallbackException)
        {
            failures++;
            // Never print database connection strings, credentials, or raw player payloads.
            Console.Error.WriteLine($"RETAINED {Path.GetFileName(path)}: {exception.GetType().Name}");
        }
    }
    return failures == 0 ? 0 : 1;
}
