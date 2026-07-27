using System.Text.Json.Serialization;

namespace Glance.Presence.WinUI;

[JsonSerializable(typeof(PresenceSettings))]
internal sealed partial class PresenceJsonContext :
    JsonSerializerContext;
