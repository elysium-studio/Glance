using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeSwitcherComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly GlanceModuleOptions<ThemeSwitcherSettings> options;
    private readonly DispatcherQueueTimer timer;
    private readonly ThemeSwitcherViewModel viewModel;
    private readonly IWritableOptions<ThemeSwitcherSettings> writer;

    public ThemeSwitcherComponent(ThemeSwitcherViewModel viewModel,
        GlanceModuleOptions<ThemeSwitcherSettings> options,
        IWritableOptions<ThemeSwitcherSettings> writer,
        ModuleResourceTextLocalizer<ThemeSwitcherModule> localizer)
    {
        this.viewModel = viewModel;
        this.options = options;
        this.writer = writer;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ThemeSwitcherCompactView compactView = new(viewModel);
        ThemeSwitcherExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();

        viewModel.SettingsChanged += HandleSettingsChanged;
        options.Changed += HandleOptionsChanged;
        _ = InitializeAsync();
    }

    public string Id => "ThemeSwitcher";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 190;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("ThemeSwitcher.Light", Id, "Use light theme", "Switch Windows apps and system surfaces to light appearance.")
        {
            SemanticTags = ["theme", "appearance", "light", "light mode", "bright", "Windows theme"],
            ExampleUtterances = ["switch to light mode", "make Windows use the light theme", "turn dark mode off"]
        },
        new GlanceActionDescriptor("ThemeSwitcher.Dark", Id, "Use dark theme", "Switch Windows apps and system surfaces to dark appearance.")
        {
            SemanticTags = ["theme", "appearance", "dark", "dark mode", "night", "Windows theme"],
            ExampleUtterances = ["switch to dark mode", "make Windows dark", "turn dark theme on"]
        },
        new GlanceActionDescriptor("ThemeSwitcher.Sunset", Id, "Use sunset theme schedule", "Automatically use light appearance during the day and dark appearance after sunset.")
        {
            SemanticTags = ["theme", "appearance", "automatic", "schedule", "sunset", "sunrise", "day", "night"],
            ExampleUtterances = ["change theme automatically at sunset", "use light by day and dark at night", "turn on the sunset theme schedule"]
        }
    ];

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        Task operation = request.ActionId switch
        {
            "ThemeSwitcher.Light" => viewModel.SelectLightAsync(),
            "ThemeSwitcher.Dark" => viewModel.SelectDarkAsync(),
            "ThemeSwitcher.Sunset" => viewModel.SelectSunsetAsync(),
            _ => Task.CompletedTask
        };

        if (request.ActionId is not ("ThemeSwitcher.Light" or "ThemeSwitcher.Dark" or "ThemeSwitcher.Sunset"))
        {
            return GlanceActionResult.Unavailable();
        }

        await operation;
        return GlanceActionResult.Success();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.SettingsChanged -= HandleSettingsChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private async Task InitializeAsync()
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch
        { }
    }

    private async void HandleTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            await viewModel.RefreshAsync();
        }
        catch
        { }
    }

    private async void HandleSettingsChanged(object? sender, EventArgs args)
    {
        try
        {
            await writer.WriteAsync(settings => viewModel.WriteSettings(settings));
        }
        catch
        { }
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ThemeSwitcherSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));
}
