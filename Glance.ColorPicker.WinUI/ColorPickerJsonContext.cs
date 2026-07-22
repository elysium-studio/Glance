using System.Text.Json.Serialization;

namespace Glance.ColorPicker.WinUI;

[JsonSerializable(typeof(ColorPickerSettings))]
internal partial class ColorPickerJsonContext : JsonSerializerContext;
