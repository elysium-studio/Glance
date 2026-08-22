using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Glance.Settings;

internal sealed class JsonGlanceSettingsStore<TOptions> :
    IWritableOptions<TOptions>
    where TOptions : class, new()
{
    private const string DataPropertyName = "data";
    private const string SchemaPropertyName = "$schema";
    private const string VersionPropertyName = "schemaVersion";
    private readonly string backupPath;
    private readonly string filePath;
    private readonly ILogger<JsonGlanceSettingsStore<TOptions>> logger;
    private readonly IReadOnlyDictionary<int, IGlanceSettingsMigration<TOptions>> migrations;
    private readonly GlanceSettingsRegistration<TOptions> registration;
    private readonly SemaphoreSlim semaphore;

    public JsonGlanceSettingsStore(IHostEnvironment environment, GlanceSettingsRegistration<TOptions> registration, IEnumerable<IGlanceSettingsMigration<TOptions>> migrations, ILogger<JsonGlanceSettingsStore<TOptions>> logger)
    {
        this.registration = registration;
        this.logger = logger;
        filePath = Path.Combine(environment.ContentRootPath, registration.FilePath);
        backupPath = $"{filePath}.bak";
        semaphore = GlanceSettingsFileLock.Get(filePath);
        this.migrations = BuildMigrationMap(migrations);
    }

    public async Task<TOptions?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            SettingsReadResult result = await ReadCurrentAsync(cancellationToken).ConfigureAwait(false);

            if (result.RequiresWrite)
            {
                await WriteDocumentAsync(result.Settings, cancellationToken).ConfigureAwait(false);
            }

            return result.Settings;
        }
        finally
        {
            _ = semaphore.Release();
        }
    }

    public async Task WriteAsync(Action<TOptions> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            SettingsReadResult result = await ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
            update(result.Settings);
            await WriteDocumentAsync(result.Settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = semaphore.Release();
        }
    }

    public async Task WriteAsync(TOptions value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _ = await ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
            await WriteDocumentAsync(value, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = semaphore.Release();
        }
    }

    private static IReadOnlyDictionary<int, IGlanceSettingsMigration<TOptions>> BuildMigrationMap(IEnumerable<IGlanceSettingsMigration<TOptions>> migrations)
    {
        Dictionary<int, IGlanceSettingsMigration<TOptions>> result = [];

        foreach (IGlanceSettingsMigration<TOptions> migration in migrations)
        {
            if (migration.FromVersion < 1 || migration.ToVersion <= migration.FromVersion)
            {
                throw new GlanceSettingsException($"The settings migration from version {migration.FromVersion} to {migration.ToVersion} is invalid.");
            }

            if (!result.TryAdd(migration.FromVersion, migration))
            {
                throw new GlanceSettingsException($"More than one settings migration starts at version {migration.FromVersion}.");
            }
        }

        return result;
    }

    private async Task<SettingsReadResult> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            if (File.Exists(backupPath))
            {
                return await RecoverFromBackupAsync(null, cancellationToken).ConfigureAwait(false);
            }

            return new SettingsReadResult(new TOptions(), false);
        }

        string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        try
        {
            return ReadDocument(content);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException or GlanceSettingsCorruptionException)
        {
            return await RecoverFromBackupAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }

    private SettingsReadResult ReadDocument(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' is empty.");
        }

        JsonObject root = JsonNode.Parse(content) as JsonObject ?? throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' does not contain a JSON object.");
        bool versioned = root.ContainsKey(SchemaPropertyName) || root.ContainsKey(VersionPropertyName) || root.ContainsKey(DataPropertyName);
        int version;
        JsonObject data;

        if (versioned)
        {
            string? schemaId = root[SchemaPropertyName]?.GetValue<string>();

            if (!string.Equals(schemaId, registration.SchemaId, StringComparison.Ordinal))
            {
                throw new GlanceSettingsCompatibilityException($"The settings file '{filePath}' belongs to schema '{schemaId ?? "unknown"}' instead of '{registration.SchemaId}'.");
            }

            version = root[VersionPropertyName]?.GetValue<int>() ?? throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' does not contain a schema version.");
            data = root[DataPropertyName] as JsonObject ?? throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' does not contain a settings object.");
        }
        else
        {
            version = 1;
            data = root[registration.SectionPath] as JsonObject ?? root;
        }

        if (version > registration.Version)
        {
            throw new GlanceSettingsCompatibilityException($"The settings file '{filePath}' uses schema version {version}, but this version of the module only supports version {registration.Version}.");
        }

        if (version < 1)
        {
            throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' contains the invalid schema version {version}.");
        }

        JsonObject migrated = data.DeepClone().AsObject();
        int currentVersion = version;

        while (currentVersion < registration.Version)
        {
            if (!migrations.TryGetValue(currentVersion, out IGlanceSettingsMigration<TOptions>? migration))
            {
                throw new GlanceSettingsMigrationException($"No settings migration is registered from schema version {currentVersion} for '{registration.SchemaId}'.");
            }

            try
            {
                migrated = migration.Migrate(migrated) ?? throw new GlanceSettingsMigrationException($"The settings migration from version {migration.FromVersion} returned no settings.");
            }
            catch (GlanceSettingsMigrationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new GlanceSettingsMigrationException($"The settings migration from version {migration.FromVersion} to {migration.ToVersion} failed.", exception);
            }

            currentVersion = migration.ToVersion;

            if (currentVersion > registration.Version)
            {
                throw new GlanceSettingsMigrationException($"The settings migration from version {migration.FromVersion} advanced beyond the supported schema version {registration.Version}.");
            }
        }

        JsonTypeInfo<TOptions> typeInfo = (JsonTypeInfo<TOptions>)registration.JsonOptions.GetTypeInfo(typeof(TOptions));
        TOptions settings = migrated.Deserialize(typeInfo) ?? throw new GlanceSettingsCorruptionException($"The settings file '{filePath}' did not produce a settings value.");
        return new SettingsReadResult(settings, !versioned || currentVersion != version);
    }

    private async Task<SettingsReadResult> RecoverFromBackupAsync(Exception? originalException, CancellationToken cancellationToken)
    {
        if (!File.Exists(backupPath))
        {
            throw new GlanceSettingsException($"The settings file '{filePath}' could not be read and no valid backup is available.", originalException ?? new FileNotFoundException("The settings file is missing.", filePath));
        }

        string backupContent = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
        SettingsReadResult result;

        try
        {
            result = ReadDocument(backupContent);
        }
        catch (Exception backupException) when (backupException is JsonException or InvalidOperationException or NotSupportedException or GlanceSettingsCorruptionException)
        {
            throw new GlanceSettingsException($"Neither the settings file '{filePath}' nor its backup could be read.", new AggregateException(originalException ?? new FileNotFoundException("The settings file is missing.", filePath), backupException));
        }

        await RestoreBackupAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(originalException, "Recovered settings from {BackupPath}; the unreadable file was preserved", backupPath);
        return result;
    }

    private async Task RestoreBackupAsync(CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            string corruptPath = $"{filePath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(filePath, corruptPath, false);
        }

        string temporaryPath = CreateTemporaryPath();

        try
        {
            await CopyFileAsync(backupPath, temporaryPath, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, filePath, false);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task WriteDocumentAsync(TOptions settings, CancellationToken cancellationToken)
    {
        string content = SerializeDocument(settings);
        _ = ReadDocument(content);
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string temporaryPath = CreateTemporaryPath();

        try
        {
            await WriteFileAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, filePath, false);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private string SerializeDocument(TOptions settings)
    {
        JsonTypeInfo<TOptions> typeInfo = (JsonTypeInfo<TOptions>)registration.JsonOptions.GetTypeInfo(typeof(TOptions));
        JsonNode? data = JsonSerializer.SerializeToNode(settings, typeInfo);
        JsonObject root = new()
        {
            [SchemaPropertyName] = registration.SchemaId,
            [VersionPropertyName] = registration.Version,
            [DataPropertyName] = data
        };
        return root.ToJsonString(registration.JsonOptions);
    }

    private string CreateTemporaryPath() => Path.Combine(Path.GetDirectoryName(filePath)!, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(true);
    }

    private static async Task WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false), 1024, true);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record SettingsReadResult(TOptions Settings, bool RequiresWrite);
}
