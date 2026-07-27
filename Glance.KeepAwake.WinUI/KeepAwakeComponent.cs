using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using System;
using System.Threading.Tasks;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ITextLocalizer localizer;
    private readonly KeepAwakeViewModel viewModel;
    private readonly IWritableOptions<KeepAwakeSettings> writer;

    public KeepAwakeComponent(KeepAwakeViewModel viewModel,
        GlanceModuleOptions<KeepAwakeSettings> options,
        IWritableOptions<KeepAwakeSettings> writer,
        ModuleResourceTextLocalizer<KeepAwakeModule> localizer)
    {
        this.viewModel = viewModel;
        this.writer = writer;
        this.localizer = localizer;

        KeepAwakeCompactView compactView = new(viewModel);
        KeepAwakeExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.SessionStateChanged += HandleSessionStateChanged;
        _ = viewModel.RestoreAsync(options.Current.ResumeAutomatically && options.Current.WasActive);
    }

    public string Id => "KeepAwake";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 170;

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
