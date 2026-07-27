using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using System;
using System.Threading.Tasks;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ITextLocalizer localizer;
    private readonly PresenceViewModel viewModel;
    private readonly IWritableOptions<PresenceSettings> writer;

    public PresenceComponent(PresenceViewModel viewModel,
        GlanceModuleOptions<PresenceSettings> options,
        IWritableOptions<PresenceSettings> writer,
        ModuleResourceTextLocalizer<PresenceModule> localizer)
    {
        this.viewModel = viewModel;
        this.writer = writer;
        this.localizer = localizer;

        PresenceCompactView compactView = new(viewModel);
        PresenceExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.SessionStateChanged += HandleSessionStateChanged;
        _ = viewModel.RestoreAsync(options.Current.ResumeAutomatically && options.Current.WasActive);
    }

    public string Id => "Presence";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 180;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        viewModel.SessionStateChanged -= HandleSessionStateChanged;
        GC.SuppressFinalize(this);
    }

    private async void HandleSessionStateChanged(object? sender, EventArgs args)
    {
        try
        {
            await writer.WriteAsync(settings => settings.WasActive = viewModel.IsActive);
        }
        catch
        { }
    }
}
