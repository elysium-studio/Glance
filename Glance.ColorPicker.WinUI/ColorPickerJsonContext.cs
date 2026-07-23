using System.Text.Json.Serialization;

namespace Glance.ColorPicker.WinUI;

[JsonSerializable(typeof(ColorPickerSettings))]
internal sealed partial class ColorPickerJsonContext : JsonSerializerContext;
