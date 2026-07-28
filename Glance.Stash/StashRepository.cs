using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Glance.Stash;

public sealed class StashRepository
{
    private const int ItemLimit = 24;
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public StashRepository(string databasePath)
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

    public IReadOnlyList<StashEntry> Load()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, content, created_at
            FROM stash_items
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", ItemLimit);
        using SqliteDataReader reader = command.ExecuteReader();
        List<StashEntry> entries = [];

        while (reader.Read())
        {
            if (Enum.IsDefined((StashItemKind)reader.GetInt32(1)) &&
                DateTimeOffset.TryParse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset createdAt))
            {
                entries.Add(new StashEntry(reader.GetString(0), (StashItemKind)reader.GetInt32(1), reader.GetString(2), createdAt));
            }
        }

        return entries;
    }

    public void Save(StashEntry entry)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO stash_items (id, kind, content, created_at)
            VALUES ($id, $kind, $content, $createdAt)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                content = excluded.content,
                created_at = excluded.created_at;

            DELETE FROM stash_items
            WHERE id IN (
                SELECT id
                FROM stash_items
                ORDER BY created_at DESC
                LIMIT -1 OFFSET $limit
            );
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$kind", (int)entry.Kind);
        command.Parameters.AddWithValue("$content", entry.Content);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$limit", ItemLimit);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void Remove(string id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM stash_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS stash_items (
                id TEXT NOT NULL PRIMARY KEY,
                kind INTEGER NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_stash_items_created_at
            ON stash_items(created_at DESC);
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

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            providerInitialized = true;
        }
    }
}
