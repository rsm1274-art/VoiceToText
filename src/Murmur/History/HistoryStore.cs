using Microsoft.Data.Sqlite;

namespace Murmur.History;

/// <summary>
/// Persists dictated text to a local SQLite file at
/// %AppData%\Murmur\history.db so it survives restarts and can be browsed.
/// Opens a short-lived connection per operation rather than holding one open
/// for the app's lifetime, since writes are infrequent (one per dictation)
/// and this avoids needing to manage connection lifetime/disposal elsewhere.
///
/// History is a convenience feature, not core to dictation: any failure here
/// (disk full, permissions, corrupt file) degrades to IsAvailable = false /
/// silently-skipped writes rather than surfacing as a dictation failure.
/// </summary>
public sealed class HistoryStore
{
    private readonly string? _connectionString;

    public bool IsAvailable { get; }

    public HistoryStore()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Murmur");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "history.db");
            _connectionString = $"Data Source={dbPath}";

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS Transcriptions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Text TEXT NOT NULL,
                    TimestampUtc TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();

            IsAvailable = true;
        }
        catch (Exception)
        {
            _connectionString = null;
            IsAvailable = false;
        }
    }

    public void Add(string text)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Transcriptions (Text, TimestampUtc) VALUES ($text, $timestamp);";
            command.Parameters.AddWithValue("$text", text);
            command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Best-effort: a failed history write must not surface as a
            // dictation failure to the user.
        }
    }

    public IReadOnlyList<TranscriptionRecord> GetAll()
    {
        var results = new List<TranscriptionRecord>();
        if (!IsAvailable)
        {
            return results;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Text, TimestampUtc FROM Transcriptions ORDER BY Id DESC;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new TranscriptionRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    DateTime.Parse(reader.GetString(2)).ToUniversalTime()));
            }
        }
        catch (Exception)
        {
            // Return whatever was read so far rather than throwing out of a
            // window-open action.
        }

        return results;
    }
}
