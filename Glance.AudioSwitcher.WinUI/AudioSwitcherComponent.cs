using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.AudioSwitcher.WinUI;

public sealed partial class AudioSwitcherComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IAudioDeviceService audioDeviceService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly AudioSwitcherViewModel viewModel;

    public AudioSwitcherComponent(AudioSwitcherViewModel viewModel,
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

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("AudioSwitcher.SelectOutput",
            Id,
            "Switch audio output",
            "Select the default audio output device.",
            [new GlanceActionParameterDescriptor("device", GlanceActionParameterType.String, "Part or all of the output device name.")])
    ];

    public bool IsAvailable(string actionId) => viewModel.HasDevices;

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? device = request.GetString("device");
        return Task.FromResult(device is not null && viewModel.SelectDevice(device)
            ? GlanceActionResult.Success($"Using {viewModel.CurrentDeviceName}.")
            : GlanceActionResult.InvalidArguments("The requested audio output is not available."));
    }

    public void Dispose() =>
        audioDeviceService.DevicesChanged -= HandleDevicesChanged;

    private void HandleDevicesChanged(object? sender, EventArgs args) =>
        dispatcherQueue.TryEnqueue(viewModel.Refresh);
}
