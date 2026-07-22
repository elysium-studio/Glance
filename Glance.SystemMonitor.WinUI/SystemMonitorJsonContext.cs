using System.Text.Json.Serialization;

namespace Glance.SystemMonitor.WinUI;

[JsonSerializable(typeof(SystemMonitorSettings))]
internal partial class SystemMonitorJsonContext : JsonSerializerContext;
