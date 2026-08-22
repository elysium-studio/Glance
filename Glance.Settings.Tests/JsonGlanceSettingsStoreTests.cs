using Glance.Application.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Glance.Settings.Tests;

public sealed class JsonGlanceSettingsStoreTests :
    IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Glance.Settings.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ReadAsyncWrapsLegacySettingsWithoutChangingValues()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {
              "Test": {
                "Name": "Preserved",
                "Count": 7
              }
            }
            """);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore();

        TestSettings? settings = await store.ReadAsync();

        Assert.NotNull(settings);
        Assert.Equal("Preserved", settings.Name);
        Assert.Equal(7, settings.Count);
        JsonObject root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        Assert.Equal("glance.test.settings", root["$schema"]!.GetValue<string>());
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Preserved", root["data"]!["Name"]!.GetValue<string>());
        Assert.True(File.Exists($"{path}.bak"));
    }

    [Fact]
    public async Task ReadAsyncRunsEveryMigrationInOrder()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {
              "Test": {
                "Value": "Migrated"
              }
            }
            """);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore(3, new RenameValueMigration(), new AddCountMigration());

        TestSettings? settings = await store.ReadAsync();

        Assert.NotNull(settings);
        Assert.Equal("Migrated", settings.Name);
        Assert.Equal(12, settings.Count);
        JsonObject root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        Assert.Equal(3, root["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task ReadAsyncDoesNotOverwriteSettingsWhenMigrationIsMissing()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        string original = """
            {
              "Test": {
                "Name": "Keep me"
              }
            }
            """;
        await File.WriteAllTextAsync(path, original);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore(2);

        _ = await Assert.ThrowsAsync<GlanceSettingsMigrationException>(() => store.ReadAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadAsyncRejectsNewerSettingsWithoutOverwritingThem()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        string original = """
            {
              "$schema": "glance.test.settings",
              "schemaVersion": 4,
              "data": {
                "Name": "From the future",
                "Count": 4
              }
            }
            """;
        await File.WriteAllTextAsync(path, original);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore(3, new RenameValueMigration(), new AddCountMigration());

        _ = await Assert.ThrowsAsync<GlanceSettingsCompatibilityException>(() => store.ReadAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadAsyncRejectsInvalidSchemaVersionWithoutOverwritingIt()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        string original = """
            {
              "$schema": "glance.test.settings",
              "schemaVersion": 0,
              "data": {
                "Name": "Keep me"
              }
            }
            """;
        await File.WriteAllTextAsync(path, original);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore();

        _ = await Assert.ThrowsAsync<GlanceSettingsException>(() => store.ReadAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadAsyncDoesNotRollBackNewerSettingsWhenBackupIsOlder()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        string backup = """
            {
              "$schema": "glance.test.settings",
              "schemaVersion": 3,
              "data": {
                "Name": "Older",
                "Count": 3
              }
            }
            """;
        string current = """
            {
              "$schema": "glance.test.settings",
              "schemaVersion": 4,
              "data": {
                "Name": "Newer",
                "Count": 4
              }
            }
            """;
        await File.WriteAllTextAsync(path, current);
        await File.WriteAllTextAsync($"{path}.bak", backup);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore(3, new RenameValueMigration(), new AddCountMigration());

        _ = await Assert.ThrowsAsync<GlanceSettingsCompatibilityException>(() => store.ReadAsync());

        Assert.Equal(current, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadAsyncDoesNotRestoreBackupWhenMigrationFails()
    {
        string path = GetPath();
        Directory.CreateDirectory(directory);
        string current = """
            {
              "Test": {
                "Value": "Current"
              }
            }
            """;
        string backup = """
            {
              "Test": {
                "Value": "Backup"
              }
            }
            """;
        await File.WriteAllTextAsync(path, current);
        await File.WriteAllTextAsync($"{path}.bak", backup);
        JsonGlanceSettingsStore<TestSettings> store = CreateStore(2, new FailingMigration());

        _ = await Assert.ThrowsAsync<GlanceSettingsMigrationException>(() => store.ReadAsync());

        Assert.Equal(current, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadAsyncRecoversLastValidBackupAndPreservesCorruptFile()
    {
        string path = GetPath();
        JsonGlanceSettingsStore<TestSettings> store = CreateStore();
        await store.WriteAsync(new TestSettings { Name = "First", Count = 1 });
        await store.WriteAsync(new TestSettings { Name = "Second", Count = 2 });
        await File.WriteAllTextAsync(path, "{ broken");

        TestSettings? settings = await store.ReadAsync();

        Assert.NotNull(settings);
        Assert.Equal("First", settings.Name);
        Assert.Equal(1, settings.Count);
        Assert.NotEmpty(Directory.EnumerateFiles(directory, "settings.dat.corrupt-*"));
        Assert.Contains("\"First\"", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriteAsyncPreservesPreviousValidDocumentAsBackup()
    {
        string path = GetPath();
        JsonGlanceSettingsStore<TestSettings> store = CreateStore();
        await store.WriteAsync(new TestSettings { Name = "Before", Count = 1 });

        await store.WriteAsync(settings =>
        {
            settings.Name = "After";
            settings.Count = 2;
        });

        Assert.Contains("\"After\"", await File.ReadAllTextAsync(path));
        Assert.Contains("\"Before\"", await File.ReadAllTextAsync($"{path}.bak"));
    }

    [Fact]
    public async Task WriteAsyncSerializesConcurrentUpdatesAcrossStoreInstances()
    {
        JsonGlanceSettingsStore<TestSettings> first = CreateStore();
        JsonGlanceSettingsStore<TestSettings> second = CreateStore();
        await first.WriteAsync(new TestSettings());

        Task[] updates = [.. Enumerable.Range(0, 40).Select(index => (index & 1) == 0
            ? first.WriteAsync(settings => settings.Count++)
            : second.WriteAsync(settings => settings.Count++))];
        await Task.WhenAll(updates);

        TestSettings? settings = await first.ReadAsync();
        Assert.NotNull(settings);
        Assert.Equal(40, settings.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private JsonGlanceSettingsStore<TestSettings> CreateStore(int version = 1, params IGlanceSettingsMigration<TestSettings>[] migrations)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = true
        };
        GlanceSettingsRegistration<TestSettings> registration = new("glance.test.settings", "Test", "settings.dat", options)
        {
            Version = version
        };
        TestHostEnvironment environment = new(directory);
        return new JsonGlanceSettingsStore<TestSettings>(environment, registration, migrations, NullLogger<JsonGlanceSettingsStore<TestSettings>>.Instance);
    }

    private string GetPath() => Path.Combine(directory, "settings.dat");

    private sealed class AddCountMigration :
        IGlanceSettingsMigration<TestSettings>
    {
        public int FromVersion => 2;

        public int ToVersion => 3;

        public JsonObject Migrate(JsonObject settings)
        {
            settings["Count"] = 12;
            return settings;
        }
    }

    private sealed class RenameValueMigration :
        IGlanceSettingsMigration<TestSettings>
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public JsonObject Migrate(JsonObject settings)
        {
            settings["Name"] = settings["Value"]?.DeepClone();
            _ = settings.Remove("Value");
            return settings;
        }
    }

    private sealed class FailingMigration :
        IGlanceSettingsMigration<TestSettings>
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public JsonObject Migrate(JsonObject settings) => throw new InvalidOperationException("Failed migration");
    }

    private sealed class TestHostEnvironment(string contentRootPath) :
        IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Glance.Settings.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string EnvironmentName { get; set; } = Environments.Development;
    }

    private sealed class TestSettings
    {
        public int Count { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
