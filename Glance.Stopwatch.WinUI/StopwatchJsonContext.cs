using System.Text.Json.Serialization;

namespace Glance.Stopwatch.WinUI;

[JsonSerializable(typeof(StopwatchSettings))]
internal sealed partial class StopwatchJsonContext :
    JsonSerializerContext;
