using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.AudioSwitcher.WinUI;

public sealed class AudioSwitcherComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IAudioDeviceService audioDeviceService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly AudioSwitcherViewModel viewModel;

    public AudioSwitcherComponent(
        AudioSwitcherViewModel viewModel,
        IAudioDeviceService audioDeviceService,
        ModuleResourceTextLocalizer<AudioSwitcherModule> localizer)
    {
        this.viewModel = viewModel;
        this.audioDeviceService = audioDeviceService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        AudioSwitcherCompactView compactView = new(viewModel);
        AudioSwitcherExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        audioDeviceService.DevicesChanged += HandleDevicesChanged;
    }

    public string Id => "AudioSwitcher";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 80;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose() =>
        audioDeviceService.DevicesChanged -= HandleDevicesChanged;

    private void HandleDevicesChanged(object? sender, EventArgs args) =>
        dispatcherQueue.TryEnqueue(viewModel.Refresh);
}
