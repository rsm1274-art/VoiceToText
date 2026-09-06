using Microsoft.Data.Sqlite;

namespace Murmur.Dictionary;

/// <summary>
/// Persists user-defined term corrections at %AppData%\Murmur\dictionary.db,
/// same short-lived-connection-per-operation pattern as HistoryStore. Each
/// entry is a correct Term plus a comma-separated list of common
/// mis-recognitions (Aliases) that DictionaryCorrector replaces with it.
/// Degrades to IsAvailable = false / no-op on failure rather than blocking
/// dictation.
/// </summary>
public sealed class CustomDictionaryStore
{
    private readonly string? _connectionString;

    public bool IsAvailable { get; }

    public CustomDictionaryStore()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Murmur");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "dictionary.db");
            _connectionString = $"Data Source={dbPath}";

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS DictionaryEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Term TEXT NOT NULL,
                    AliasesCsv TEXT NOT NULL
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

    public void Add(string term, string aliasesCsv)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO DictionaryEntries (Term, AliasesCsv) VALUES ($term, $aliases);";
            command.Parameters.AddWithValue("$term", term);
            command.Parameters.AddWithValue("$aliases", aliasesCsv);
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Best-effort; a failed write shouldn't block dictation.
        }
    }

    public void Delete(long id)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DictionaryEntries WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Best-effort.
        }
    }

    public IReadOnlyList<DictionaryEntry> GetAll()
    {
        var results = new List<DictionaryEntry>();
        if (!IsAvailable)
        {
            return results;
        }

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Term, AliasesCsv FROM DictionaryEntries ORDER BY Term;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new DictionaryEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2)));
            }
        }
        catch (Exception)
        {
            // Return whatever was read so far rather than throwing.
        }

        return results;
    }
}
