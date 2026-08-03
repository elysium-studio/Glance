using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Glance.Reminders;

public sealed class ReminderRepository
{
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public ReminderRepository(string databasePath)
    {
        InitializeProvider();
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? throw new ArgumentException("The database path must include a directory.", nameof(databasePath)));
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        InitializeDatabase();
    }

    public IReadOnlyList<ReminderEntry> Load()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, due_at, priority, created_at
            FROM reminders
            ORDER BY due_at ASC, priority DESC, created_at ASC;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<ReminderEntry> entries = [];

        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dueAt) &&
                DateTimeOffset.TryParse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset createdAt) &&
                Enum.IsDefined((ReminderPriority)reader.GetInt32(3)))
            {
                entries.Add(new ReminderEntry(reader.GetString(0), reader.GetString(1), dueAt, (ReminderPriority)reader.GetInt32(3), createdAt));
            }
        }

        return entries;
    }

    public void Save(ReminderEntry entry)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO reminders (id, title, due_at, priority, created_at)
            VALUES ($id, $title, $dueAt, $priority, $createdAt)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                due_at = excluded.due_at,
                priority = excluded.priority;
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$title", entry.Title);
        command.Parameters.AddWithValue("$dueAt", entry.DueAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$priority", (int)entry.Priority);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM reminders WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS reminders (
                id TEXT NOT NULL PRIMARY KEY,
                title TEXT NOT NULL,
                due_at TEXT NOT NULL,
                priority INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_reminders_due_at_priority
            ON reminders(due_at ASC, priority DESC);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 3000;";
        command.ExecuteNonQuery();
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
