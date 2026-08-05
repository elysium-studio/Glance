using Microsoft.Data.Sqlite;

namespace Glance.ColorPicker;

public sealed class ColorHistoryRepository
{
    private static readonly Lock providerLock = new();
    private static bool providerInitialized;
    private readonly string connectionString;

    public ColorHistoryRepository(string databasePath)
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

    public IReadOnlyList<ColorValue> Load(int limit)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT red, green, blue
            FROM recent_colors
            ORDER BY sort_order DESC
            LIMIT $limit;
            """;
        _ = command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        using SqliteDataReader reader = command.ExecuteReader();
        List<ColorValue> colors = [];

        while (reader.Read())
        {
            colors.Add(new ColorValue(reader.GetByte(0), reader.GetByte(1), reader.GetByte(2)));
        }

        return colors;
    }

    public void Save(ColorValue color,
        int limit)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO recent_colors (packed_rgb, red, green, blue, sort_order)
            VALUES (
                $packedRgb, $red, $green, $blue,
                (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM recent_colors))
            ON CONFLICT(packed_rgb) DO UPDATE SET
                sort_order = excluded.sort_order;
            """;
        _ = command.Parameters.AddWithValue("$packedRgb", (color.Red << 16) | (color.Green << 8) | color.Blue);
        _ = command.Parameters.AddWithValue("$red", color.Red);
        _ = command.Parameters.AddWithValue("$green", color.Green);
        _ = command.Parameters.AddWithValue("$blue", color.Blue);
        _ = command.ExecuteNonQuery();
        Trim(connection, transaction, limit);
        transaction.Commit();
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

            CREATE TABLE IF NOT EXISTS recent_colors (
                packed_rgb INTEGER NOT NULL PRIMARY KEY,
                red INTEGER NOT NULL,
                green INTEGER NOT NULL,
                blue INTEGER NOT NULL,
                sort_order INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_recent_colors_sort_order
            ON recent_colors(sort_order DESC);
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

    private static void Trim(SqliteConnection connection,
        SqliteTransaction transaction,
        int limit)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM recent_colors
            WHERE packed_rgb IN (
                SELECT packed_rgb
                FROM recent_colors
                ORDER BY sort_order DESC
                LIMIT -1 OFFSET $limit
            );
            """;
        _ = command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        _ = command.ExecuteNonQuery();
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
