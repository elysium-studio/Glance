using System.Text.Json.Serialization;

namespace Glance.FocusSession.WinUI;

[JsonSerializable(typeof(FocusSessionSettings))]
internal partial class FocusSessionJsonContext : JsonSerializerContext;
