using System.Text.Json.Serialization;

namespace Glance.Timer.WinUI;

[JsonSerializable(typeof(TimerSettings))]
internal sealed partial class TimerJsonContext : JsonSerializerContext;
