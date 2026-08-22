using System.Text.Json.Serialization;

namespace Glance.Hydration.WinUI;

[JsonSerializable(typeof(HydrationSettings))]
internal sealed partial class HydrationJsonContext :
    JsonSerializerContext;
