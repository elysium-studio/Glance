using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Glance.ScreenCapture;

public sealed class ScreenCaptureRepository
{
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public ScreenCaptureRepository(string databasePath)
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

    public IReadOnlyList<ScreenCaptureItem> Load()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT file_path, file_name, captured_at, width, height, capture_mode
            FROM screen_captures
            ORDER BY captured_at DESC;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<ScreenCaptureItem> captures = [];

        while (reader.Read())
        {
            if (DateTimeOffset.TryParse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset capturedAt) &&
                Enum.IsDefined((ScreenCaptureMode)reader.GetInt32(5)))
            {
                captures.Add(new ScreenCaptureItem(reader.GetString(0),
                    reader.GetString(1),
                    capturedAt,
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    (ScreenCaptureMode)reader.GetInt32(5)));
            }
        }

        return captures;
    }

    public void Save(ScreenCaptureItem capture)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO screen_captures (
                file_path, file_name, captured_at, width, height, capture_mode)
            VALUES (
                $filePath, $fileName, $capturedAt, $width, $height, $captureMode)
            ON CONFLICT(file_path) DO UPDATE SET
                file_name = excluded.file_name,
                captured_at = excluded.captured_at,
                width = excluded.width,
                height = excluded.height,
                capture_mode = excluded.capture_mode;
            """;
        command.Parameters.AddWithValue("$filePath", capture.FilePath);
        command.Parameters.AddWithValue("$fileName", capture.FileName);
        command.Parameters.AddWithValue("$capturedAt", capture.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$width", capture.Width);
        command.Parameters.AddWithValue("$height", capture.Height);
        command.Parameters.AddWithValue("$captureMode", (int)capture.Mode);
        command.ExecuteNonQuery();
    }

    public void Remove(string filePath)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM screen_captures WHERE file_path = $filePath;";
        command.Parameters.AddWithValue("$filePath", filePath);
        command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS screen_captures (
                file_path TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                file_name TEXT NOT NULL,
                captured_at TEXT NOT NULL,
                width INTEGER NOT NULL,
                height INTEGER NOT NULL,
                capture_mode INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_screen_captures_captured_at
            ON screen_captures(captured_at DESC);
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
