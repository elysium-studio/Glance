using System.Text.Json.Serialization;

namespace Glance.WorldClock.WinUI;

[JsonSerializable(typeof(WorldClockSettings))]
internal sealed partial class WorldClockJsonContext :
    JsonSerializerContext;
