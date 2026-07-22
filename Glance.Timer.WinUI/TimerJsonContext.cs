using System.Text.Json.Serialization;

namespace Glance.Timer.WinUI;

[JsonSerializable(typeof(TimerSettings))]
internal partial class TimerJsonContext : JsonSerializerContext;
