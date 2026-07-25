using Glance.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.Infinity.WinUI;

public sealed class InfinityComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent
{
    private readonly ITextLocalizer localizer;

    public InfinityComponent(
        InfinityViewModel viewModel,
        ModuleResourceTextLocalizer<InfinityModule> localizer)
    {
        this.localizer = localizer;
        InfinityCompactView compactView = new(viewModel, localizer);
        InfinityExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "Infinity";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 150;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }
}
