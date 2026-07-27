using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeSwitcherComponent :
    IGlanceComponent,
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
