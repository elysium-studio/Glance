using System.Text.Json.Serialization;

namespace Glance.ThemeSwitcher.WinUI;

[JsonSerializable(typeof(ThemeSwitcherSettings))]
internal sealed partial class ThemeSwitcherJsonContext :
    JsonSerializerContext;
