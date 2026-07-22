using System.Text.Json.Serialization;

namespace Glance.DevicePresence.WinUI;

[JsonSerializable(typeof(DevicePresenceSettings))]
internal sealed partial class DevicePresenceJsonContext : JsonSerializerContext;
