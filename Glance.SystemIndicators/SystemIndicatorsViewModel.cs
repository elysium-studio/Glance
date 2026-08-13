using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.SystemIndicators;

public sealed partial class SystemIndicatorsViewModel :
    ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string primaryText = string.Empty;

    [ObservableProperty]
    private string secondaryText = string.Empty;

    [ObservableProperty]
    private string glyph = string.Empty;

    [ObservableProperty]
    private double level;

    [ObservableProperty]
    private bool isLevelVisible;

    public string CompactText => string.IsNullOrWhiteSpace(Title)
        ? PrimaryText
        : $"{Title} \u00B7 {PrimaryText}";

    public void Update(SystemIndicatorPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        Title = presentation.Title;
        PrimaryText = presentation.PrimaryText;
        SecondaryText = presentation.SecondaryText;
        Glyph = presentation.Glyph;
        IsLevelVisible = presentation.Level is not null;
        Level = Math.Clamp(presentation.Level ?? 0, 0, 100);
        OnPropertyChanged(nameof(CompactText));
    }
}
