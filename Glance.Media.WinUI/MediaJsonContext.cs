using System.Text.Json.Serialization;

namespace Glance.Media.WinUI;

[JsonSerializable(typeof(MediaSettings))]
internal sealed partial class MediaJsonContext : JsonSerializerContext;
