using Glance.Application.Abstractions;
using Glance.AppMixer;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.AppMixer.WinUI;

public sealed class AppMixerComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly ITextLocalizer localizer;
    private readonly AppMixerViewModel viewModel;

    public AppMixerComponent(AppMixerViewModel viewModel,
        ModuleResourceTextLocalizer<AppMixerModule> localizer)
    {
        this.viewModel = viewModel;
        this.localizer = localizer;

        AppMixerCompactView compactView = new(viewModel);
        AppMixerExpandedView expandedView = new(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        refreshTimer = dispatcherQueue.CreateTimer();
        refreshTimer.Interval = TimeSpan.FromMilliseconds(150);
        refreshTimer.IsRepeating = true;
        refreshTimer.Tick += HandleRefreshTimerTick;
        refreshTimer.Start();
    }

    public string Id => "AppMixer";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 85;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        CreateAction("AppMixer.SetVolume", "Set app volume", "Set an application's sound volume.", true),
        CreateAction("AppMixer.Mute", "Mute app", "Mute an application without changing other sound.", false),
        CreateAction("AppMixer.Unmute", "Unmute app", "Restore sound for an application.", false)
    ];

    public bool IsAvailable(string actionId) => viewModel.HasApplications;

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? application = request.GetString("application");

        if (string.IsNullOrWhiteSpace(application))
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("Which app do you mean?", "Say the name of an app that is playing sound."));
        }

        int matches = viewModel.CountMatchingApplications(application);

        if (matches == 0)
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments($"I couldn't find an audio session for {application}.", "Play something in the app, then try again."));
        }

        if (matches > 1)
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments($"Several apps match {application}.", "Try a more specific app name."));
        }

        if (string.Equals(request.ActionId, "AppMixer.SetVolume", StringComparison.OrdinalIgnoreCase))
        {
            double? volume = request.GetNumber("volume");

            if (volume is null || volume < 0 || volume > 100)
            {
                return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("What volume should I use?", "Say a value from 0 to 100 percent."));
            }
        }

        return Task.FromResult<GlanceActionResult?>(null);
    }

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? application = request.GetString("application");

        if (application is null || !viewModel.TrySelectApplication(application) || viewModel.SelectedApplication is null)
        {
            return Task.FromResult(GlanceActionResult.InvalidArguments("The requested app is not available."));
        }

        AudioApplicationItemViewModel selected = viewModel.SelectedApplication;

        if (string.Equals(request.ActionId, "AppMixer.SetVolume", StringComparison.OrdinalIgnoreCase))
        {
            selected.Volume = request.GetNumber("volume") ?? selected.Volume;
            return Task.FromResult(GlanceActionResult.Success($"Set {selected.DisplayName} to {selected.VolumeText}."));
        }

        selected.IsMuted = string.Equals(request.ActionId, "AppMixer.Mute", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(GlanceActionResult.Success(selected.IsMuted
            ? $"Muted {selected.DisplayName}."
            : $"Unmuted {selected.DisplayName}."));
    }

    public void Dispose()
    {
        refreshTimer.Stop();
        refreshTimer.Tick -= HandleRefreshTimerTick;
    }

    private static GlanceActionDescriptor CreateAction(string id,
        string displayName,
        string description,
        bool includeVolume)
    {
        List<GlanceActionParameterDescriptor> parameters = [new("application", GlanceActionParameterType.String, "The application name.")];

        if (includeVolume)
        {
            parameters.Add(new GlanceActionParameterDescriptor("volume", GlanceActionParameterType.Number, "A volume percentage from 0 to 100."));
        }

        return new GlanceActionDescriptor(id, "AppMixer", displayName, description, parameters)
        {
            SemanticTags = ["app volume", "application sound", "volume mixer", "mute app", "sound level"],
            ExampleUtterances = includeVolume
                ? ["set Edge volume to 40 percent", "turn Spotify down to 20"]
                : ["mute Edge", "unmute Spotify"]
        };
    }

    private void HandleRefreshTimerTick(DispatcherQueueTimer sender,
        object args) => viewModel.Refresh();
}
