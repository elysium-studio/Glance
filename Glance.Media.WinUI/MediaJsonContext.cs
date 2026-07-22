using System.Text.Json.Serialization;

namespace Glance.Media.WinUI;

[JsonSerializable(typeof(MediaSettings))]
internal partial class MediaJsonContext : JsonSerializerContext;
