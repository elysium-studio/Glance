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
    IGlanceActionValidator,
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

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 80;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("AudioSwitcher.SelectOutput",
            Id,
            "Switch audio output",
            "Switch sound playback to a named speaker, headset, headphones, monitor, or other output device.",
            [new GlanceActionParameterDescriptor("device", GlanceActionParameterType.String, "Part or all of the output device name.")])
        {
            SemanticTags = ["audio output", "sound output", "speaker", "speakers", "headset", "headphones", "playback device"],
            ExampleUtterances = ["switch audio to my headphones", "use the living room speakers", "change sound output to my monitor"]
        }
    ];

    public bool IsAvailable(string actionId) => viewModel.HasDevices;

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "AudioSwitcher.SelectOutput", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        string? device = request.GetString("device");

        if (string.IsNullOrWhiteSpace(device))
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("Which output do you mean?", "Say the speaker or headset name."));
        }

        int matches = viewModel.CountMatchingDevices(device);

        return Task.FromResult<GlanceActionResult?>(matches switch
        {
            0 => GlanceActionResult.InvalidArguments($"I couldn't find “{device}”.", "Try another output device name."),
            > 1 => GlanceActionResult.InvalidArguments($"Several outputs match “{device}”.", "Try a more specific device name."),
            _ => null
        });
    }

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? device = request.GetString("device");
        return Task.FromResult(device is not null && viewModel.SelectDevice(device)
            ? GlanceActionResult.Success($"Using {viewModel.CurrentDeviceName}.")
            : GlanceActionResult.InvalidArguments("The requested audio output is not available."));
    }

    public void Dispose() => audioDeviceService.DevicesChanged -= HandleDevicesChanged;

    private void HandleDevicesChanged(object? sender, EventArgs args) => _ = dispatcherQueue.TryEnqueue(viewModel.Refresh);
}
