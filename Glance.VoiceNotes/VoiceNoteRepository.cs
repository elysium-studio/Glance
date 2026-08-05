using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Glance.VoiceNotes;

public sealed class VoiceNoteRepository
{
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public VoiceNoteRepository(string databasePath)
    {
        InitializeProvider();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? throw new ArgumentException("The database path must include a directory.", nameof(databasePath)));
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        InitializeDatabase();
    }

    public IReadOnlyList<VoiceNote> Load()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT file_path, created_at, duration_ticks
            FROM voice_notes
            ORDER BY created_at DESC;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<VoiceNote> recordings = [];

        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset createdAt))
            {
                recordings.Add(new VoiceNote(reader.GetString(0), createdAt, TimeSpan.FromTicks(reader.GetInt64(2))));
            }
        }

        return recordings;
    }

    public void Save(VoiceNote recording)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO voice_notes (file_path, created_at, duration_ticks)
            VALUES ($filePath, $createdAt, $durationTicks)
            ON CONFLICT(file_path) DO UPDATE SET
                created_at = excluded.created_at,
                duration_ticks = excluded.duration_ticks;
            """;
        _ = command.Parameters.AddWithValue("$filePath", recording.FilePath);
        _ = command.Parameters.AddWithValue("$createdAt", recording.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$durationTicks", recording.Duration.Ticks);
        _ = command.ExecuteNonQuery();
    }

    public void Remove(string filePath)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM voice_notes WHERE file_path = $filePath;";
        _ = command.Parameters.AddWithValue("$filePath", filePath);
        _ = command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS voice_notes (
                file_path TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                created_at TEXT NOT NULL,
                duration_ticks INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_voice_notes_created_at
            ON voice_notes(created_at DESC);
            """;
        _ = command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 3000;";
        _ = command.ExecuteNonQuery();
        return connection;
    }

    private static void InitializeProvider()
    {
        lock (providerLock)
        {
            if (providerInitialized)
            {
                return;
            }

            try
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            }
            catch (InvalidOperationException)
            {
            }

            providerInitialized = true;
        }
    }
}
