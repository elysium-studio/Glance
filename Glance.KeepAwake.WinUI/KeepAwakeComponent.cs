using Glance.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent
{
    private readonly ITextLocalizer localizer;

    public KeepAwakeComponent(KeepAwakeViewModel viewModel,
        ModuleResourceTextLocalizer<KeepAwakeModule> localizer)
    {
        this.localizer = localizer;

        KeepAwakeCompactView compactView = new(viewModel);
        KeepAwakeExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "KeepAwake";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 170;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }
}
