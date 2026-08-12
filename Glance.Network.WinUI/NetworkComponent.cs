using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using Windows.Networking.Connectivity;

namespace Glance.Network.WinUI;

public sealed class NetworkComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer timer;
    private readonly NetworkSnapshotReader snapshotReader;
    private readonly NetworkViewModel viewModel;
    private readonly ITextLocalizer localizer;

    public NetworkComponent(NetworkViewModel viewModel,
        NetworkSnapshotReader snapshotReader,
        ModuleResourceTextLocalizer<NetworkModule> localizer)
    {
        this.viewModel = viewModel;
        this.snapshotReader = snapshotReader;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        NetworkCompactView compactView = new(viewModel);
        NetworkExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();
        NetworkInformation.NetworkStatusChanged += HandleNetworkStatusChanged;
        UpdateNetwork();
    }

    public string Id => "Network";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Information;

    public int Order => 35;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        NetworkInformation.NetworkStatusChanged -= HandleNetworkStatusChanged;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) => UpdateNetwork();

    private void HandleNetworkStatusChanged(object sender) => _ = dispatcherQueue.TryEnqueue(UpdateNetwork);

    private void UpdateNetwork() => viewModel.Update(snapshotReader.Read());
}
