using System.Text.Json.Serialization;

namespace Glance.Shell;

[JsonSerializable(typeof(GlanceSettings))]
[JsonSerializable(typeof(GlanceModulePreference))]
[JsonSerializable(typeof(List<GlanceModulePreference>))]
public partial class GlanceJsonContext : JsonSerializerContext;
