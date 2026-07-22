using System.Text.Json.Serialization;

namespace Glance.ScreenCapture.WinUI;

[JsonSerializable(typeof(ScreenCaptureSettings))]
internal partial class ScreenCaptureJsonContext : JsonSerializerContext;
