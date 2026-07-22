using System.Text.Json.Serialization;

namespace Glance.Clipboard.WinUI;

[JsonSerializable(typeof(ClipboardSettings))]
internal partial class ClipboardJsonContext : JsonSerializerContext;
