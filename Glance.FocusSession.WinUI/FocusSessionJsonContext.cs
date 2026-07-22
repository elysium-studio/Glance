using System.Text.Json.Serialization;

namespace Glance.FocusSession.WinUI;

[JsonSerializable(typeof(FocusSessionSettings))]
internal sealed partial class FocusSessionJsonContext : JsonSerializerContext;
