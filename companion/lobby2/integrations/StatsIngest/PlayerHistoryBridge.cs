using System.Text.Json;
using Npgsql;

// Profile-local mailbox: the game never holds database credentials or makes SQL calls.
internal static class PlayerHistoryBridge
{
    public static async Task Process(string profile, NpgsqlDataSource database)
    {
        var directory = Path.Combine(profile, "Lobby2PlayerHistory");
        if (!Directory.Exists(directory)) return;
        foreach (var marker in Directory.EnumerateFiles(directory, "*.request.ready").Take(16))
        {
            var requestPath = marker[..^6];
            try
            {
                if (await File.ReadAllTextAsync(marker) != "ready") continue;
                if (new FileInfo(requestPath).Length is <= 0 or > 131072)
                    throw new InvalidDataException("Invalid history request size.");
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(requestPath));
                var root = document.RootElement;
                var token = root.GetProperty("token").GetGuid();
                var mission = root.GetProperty("missionId").GetGuid();
                var identities = root.GetProperty("identities").Deserialize<string[]>()
                    ?? throw new InvalidDataException("Missing identities.");
                if (root.GetProperty("version").GetInt32() != 1 || identities.Length > 256
                    || identities.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 256)
                    || Path.GetFileName(requestPath) != $"{token}.request")
                    throw new InvalidDataException("Invalid history request.");

                var histories = new Dictionary<string, (double Seconds, bool Known)>(StringComparer.Ordinal);
                await using var connection = await database.OpenConnectionAsync();
                // A single statement gives every requested identity the same database snapshot.
                await using (var query = new NpgsqlCommand("""
                        SELECT player_id, COALESCE(sum(play_seconds), 0), bool_and(playtime_known)
                        FROM lobby2.player_visits
                        WHERE player_id = ANY($1) AND mission_id <> $2
                        GROUP BY player_id
                        """, connection))
                {
                    query.Parameters.AddWithValue(identities);
                    query.Parameters.AddWithValue(mission);
                    await using var reader = await query.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        histories.Add(reader.GetString(0), (reader.GetDouble(1), reader.GetBoolean(2)));
                }
                var values = identities.Select(id => histories.GetValueOrDefault(id, (0d, true))).ToArray();
                var seconds = values.Select(value => value.Item1).ToArray();
                var known = values.Select(value => value.Item2).ToArray();
                var response = JsonSerializer.Serialize(new { version = 1, token, missionId = mission,
                    generatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    identities, seconds, known });
                var destination = Path.Combine(directory, $"{token}.response");
                await File.WriteAllTextAsync(destination + ".tmp", response);
                File.Move(destination + ".tmp", destination, true);
                File.Delete(marker);
                File.Delete(requestPath);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidDataException
                or NpgsqlException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                Console.Error.WriteLine($"History request retained: {error.GetType().Name}");
            }
        }
    }
}
