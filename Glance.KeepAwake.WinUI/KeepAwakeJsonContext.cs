using System.Text.Json.Serialization;

namespace Glance.KeepAwake.WinUI;

[JsonSerializable(typeof(KeepAwakeSettings))]
internal sealed partial class KeepAwakeJsonContext :
    JsonSerializerContext;
