using System.Text.Json.Serialization;

namespace Glance.Power.WinUI;

[JsonSerializable(typeof(PowerSettings))]
internal sealed partial class PowerJsonContext : JsonSerializerContext;
