using Glance.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent
{
    private readonly ITextLocalizer localizer;

    public PresenceComponent(PresenceViewModel viewModel,
        ModuleResourceTextLocalizer<PresenceModule> localizer)
    {
        this.localizer = localizer;

        PresenceCompactView compactView = new(viewModel);
        PresenceExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "Presence";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 180;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }
}
