namespace Glance.Application.Abstractions;

public interface IGlanceComponent
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    string SettingsCategory => GlanceModuleCategories.Other;

    string AccentResourceKey => "AccentTextFillColorPrimaryBrush";

    int Order { get; }

    object CompactContent { get; }

    object ExpandedContent { get; }
}
