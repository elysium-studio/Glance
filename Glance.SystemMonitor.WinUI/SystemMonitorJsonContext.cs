using System.Text.Json.Serialization;

namespace Glance.SystemMonitor.WinUI;

[JsonSerializable(typeof(SystemMonitorSettings))]
internal sealed partial class SystemMonitorJsonContext : JsonSerializerContext;
