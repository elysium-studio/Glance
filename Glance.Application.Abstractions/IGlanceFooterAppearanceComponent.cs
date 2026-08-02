namespace Glance.Application.Abstractions;

public interface IGlanceFooterAppearanceComponent
{
    uint? FooterForegroundColor { get; }

    event EventHandler? FooterAppearanceChanged;
}
