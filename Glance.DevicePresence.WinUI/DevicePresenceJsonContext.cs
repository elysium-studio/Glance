using System.Text.Json.Serialization;

namespace Glance.DevicePresence.WinUI;

[JsonSerializable(typeof(DevicePresenceSettings))]
internal partial class DevicePresenceJsonContext : JsonSerializerContext;
