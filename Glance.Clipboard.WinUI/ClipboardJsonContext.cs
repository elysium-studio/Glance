using System.Text.Json.Serialization;

namespace Glance.Clipboard.WinUI;

[JsonSerializable(typeof(ClipboardSettings))]
internal sealed partial class ClipboardJsonContext : JsonSerializerContext;
