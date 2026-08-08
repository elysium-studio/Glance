using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Shell;

public sealed partial class SetupTourModuleViewModel(string id,
    string displayName,
    string description,
    string categoryDisplayName,
    string glyph,
    string glyphFontFamily,
    string accentResourceKey,
    object? accentResourceSource,
    bool isEnabled) :
    ObservableObject
{
    [ObservableProperty]
    private bool isEnabled = isEnabled;

    public string Id { get; } = id;

    public string DisplayName { get; } = displayName;

    public string Description { get; } = description;

    public string CategoryDisplayName { get; } = categoryDisplayName;

    public string Glyph { get; } = glyph;

    public string GlyphFontFamily { get; } = glyphFontFamily;

    public string AccentResourceKey { get; } = accentResourceKey;

    public object? AccentResourceSource { get; } = accentResourceSource;
}
