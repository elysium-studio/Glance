using System.Text.Json.Serialization;

namespace Glance.Fasting.WinUI;

[JsonSerializable(typeof(FastingSettings))]
internal sealed partial class FastingJsonContext :
    JsonSerializerContext;
