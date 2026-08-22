using System.Text.Json.Nodes;

namespace Glance.Application.Abstractions;

public interface IGlanceSettingsMigration<TOptions>
    where TOptions : class, new()
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonObject Migrate(JsonObject settings);
}
