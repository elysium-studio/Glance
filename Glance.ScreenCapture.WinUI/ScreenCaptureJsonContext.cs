using System.Text.Json.Serialization;

namespace Glance.ScreenCapture.WinUI;

[JsonSerializable(typeof(ScreenCaptureSettings))]
internal sealed partial class ScreenCaptureJsonContext : JsonSerializerContext;
