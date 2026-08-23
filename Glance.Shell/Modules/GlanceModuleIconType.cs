using System.Text.Json.Serialization;

namespace Glance.Shell;

[JsonConverter(typeof(JsonStringEnumConverter<GlanceModuleIconType>))]
public enum GlanceModuleIconType
{
    [JsonStringEnumMemberName("glyph")]
    Glyph,

    [JsonStringEnumMemberName("path")]
    Path,

    [JsonStringEnumMemberName("bitmap")]
    Bitmap
}
