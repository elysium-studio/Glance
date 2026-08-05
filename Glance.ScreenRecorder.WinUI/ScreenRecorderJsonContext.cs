using System.Text.Json.Serialization;

namespace Glance.ScreenRecorder.WinUI;

[JsonSerializable(typeof(ScreenRecorderSettings))]
internal sealed partial class ScreenRecorderJsonContext :
    JsonSerializerContext;
