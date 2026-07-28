using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Glance.Clipboard;

public sealed class ClipboardRepository
{
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public ClipboardRepository(string databasePath)
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

    public IReadOnlyList<ClipboardRecord> Load(int limit)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, content_hash, captured_at, text_content, html_content, rtf_content,
                   bitmap_content, web_link, application_link
            FROM clipboard_items
            ORDER BY sort_order DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using SqliteDataReader reader = command.ExecuteReader();
        List<ClipboardRecord> records = [];

        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp))
            {
                records.Add(new ClipboardRecord(reader.GetString(0),
                    reader.GetString(1),
                    timestamp,
                    ReadString(reader, 3),
                    ReadString(reader, 4),
                    ReadString(reader, 5),
                    reader.IsDBNull(6) ? null : (byte[])reader.GetValue(6),
                    null,
                    ReadString(reader, 7),
                    ReadString(reader, 8)));
            }
        }

        reader.Close();

        for (int index = 0; index < records.Count; index++)
        {
            ClipboardRecord record = records[index];
            records[index] = record with
            {
                FilePaths = LoadFilePaths(connection, record.Id)
            };
        }

        return records;
    }

    public void Save(ClipboardRecord record,
        int limit)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO clipboard_items (
                id, content_hash, captured_at, sort_order, text_content, html_content,
                rtf_content, bitmap_content, web_link, application_link)
            VALUES (
                $id, $contentHash, $capturedAt,
                (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM clipboard_items),
                $text, $html,
                $rtf, $bitmap, $webLink, $applicationLink)
            ON CONFLICT(id) DO UPDATE SET
                content_hash = excluded.content_hash,
                captured_at = excluded.captured_at,
                sort_order = excluded.sort_order,
                text_content = excluded.text_content,
                html_content = excluded.html_content,
                rtf_content = excluded.rtf_content,
                bitmap_content = excluded.bitmap_content,
                web_link = excluded.web_link,
                application_link = excluded.application_link;

            DELETE FROM clipboard_file_paths WHERE clipboard_id = $id;
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$contentHash", record.ContentHash);
        command.Parameters.AddWithValue("$capturedAt", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$text", (object?)record.Text ?? DBNull.Value);
        command.Parameters.AddWithValue("$html", (object?)record.Html ?? DBNull.Value);
        command.Parameters.AddWithValue("$rtf", (object?)record.Rtf ?? DBNull.Value);
        command.Parameters.AddWithValue("$bitmap", (object?)record.Bitmap ?? DBNull.Value);
        command.Parameters.AddWithValue("$webLink", (object?)record.WebLink ?? DBNull.Value);
        command.Parameters.AddWithValue("$applicationLink", (object?)record.ApplicationLink ?? DBNull.Value);
        command.ExecuteNonQuery();

        if (record.FilePaths is { Count: > 0 })
        {
            using SqliteCommand fileCommand = connection.CreateCommand();
            fileCommand.Transaction = transaction;
            fileCommand.CommandText = """
                INSERT INTO clipboard_file_paths (clipboard_id, position, path)
                VALUES ($clipboardId, $position, $path);
                """;
            SqliteParameter clipboardId = fileCommand.Parameters.Add("$clipboardId", SqliteType.Text);
            SqliteParameter position = fileCommand.Parameters.Add("$position", SqliteType.Integer);
            SqliteParameter path = fileCommand.Parameters.Add("$path", SqliteType.Text);

            for (int index = 0; index < record.FilePaths.Count; index++)
            {
                clipboardId.Value = record.Id;
                position.Value = index;
                path.Value = record.FilePaths[index];
                fileCommand.ExecuteNonQuery();
            }
        }

        Trim(connection, transaction, limit);
        transaction.Commit();
    }

    public void Promote(string id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clipboard_items
            SET sort_order = (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM clipboard_items)
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Clear()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_items;";
        command.ExecuteNonQuery();
    }

    public void Trim(int limit)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        Trim(connection, transaction, limit);
        transaction.Commit();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS clipboard_items (
                id TEXT NOT NULL PRIMARY KEY,
                content_hash TEXT NOT NULL,
                captured_at TEXT NOT NULL,
                sort_order INTEGER NOT NULL,
                text_content TEXT NULL,
                html_content TEXT NULL,
                rtf_content TEXT NULL,
                bitmap_content BLOB NULL,
                web_link TEXT NULL,
                application_link TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_sort_order
            ON clipboard_items(sort_order DESC);

            CREATE INDEX IF NOT EXISTS ix_clipboard_items_content_hash
            ON clipboard_items(content_hash);

            CREATE TABLE IF NOT EXISTS clipboard_file_paths (
                clipboard_id TEXT NOT NULL,
                position INTEGER NOT NULL,
                path TEXT NOT NULL,
                PRIMARY KEY (clipboard_id, position),
                FOREIGN KEY (clipboard_id) REFERENCES clipboard_items(id) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA busy_timeout = 3000;
            PRAGMA foreign_keys = ON;
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private static IReadOnlyList<string>? LoadFilePaths(SqliteConnection connection,
        string id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT path
            FROM clipboard_file_paths
            WHERE clipboard_id = $id
            ORDER BY position;
            """;
        command.Parameters.AddWithValue("$id", id);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> paths = [];

        while (reader.Read())
        {
            paths.Add(reader.GetString(0));
        }

        return paths.Count == 0 ? null : paths;
    }

    private static string? ReadString(SqliteDataReader reader,
        int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void Trim(SqliteConnection connection,
        SqliteTransaction transaction,
        int limit)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM clipboard_items
            WHERE id IN (
                SELECT id
                FROM clipboard_items
                ORDER BY sort_order DESC
                LIMIT -1 OFFSET $limit
            );
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        command.ExecuteNonQuery();
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
