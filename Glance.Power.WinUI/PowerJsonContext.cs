using System.Text.Json.Serialization;

namespace Glance.Power.WinUI;

[JsonSerializable(typeof(PowerSettings))]
internal partial class PowerJsonContext : JsonSerializerContext;
