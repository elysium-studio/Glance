using System.Text.Json;

namespace Glance.Settings;

internal sealed class GlanceSettingsRegistration<TOptions>(string schemaId, string sectionPath, string filePath, JsonSerializerOptions jsonOptions)
    where TOptions : class, new()
{
    public string FilePath { get; } = filePath;

    public JsonSerializerOptions JsonOptions { get; } = jsonOptions;

    public string SchemaId { get; } = schemaId;

    public string SectionPath { get; } = sectionPath;

    public int Version { get; set; } = 1;
}
