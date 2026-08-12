using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using Windows.Networking.Connectivity;

namespace Glance.Network.WinUI;

public sealed class NetworkAdapterComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly NetworkAdapterViewModel viewModel;
    private readonly ITextLocalizer localizer;

    public NetworkAdapterComponent(NetworkAdapterViewModel viewModel,
        ModuleResourceTextLocalizer<NetworkModule> localizer)
    {
        this.viewModel = viewModel;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        NetworkAdapterCompactView compactView = new(viewModel);
        NetworkAdapterExpandedView expandedView = new(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        NetworkInformation.NetworkStatusChanged += HandleNetworkStatusChanged;
        viewModel.Refresh();
    }

    public string Id => "NetworkAdapter";

    public string DisplayName => localizer.GetText("NetworkAdapterDisplayName");

    public string Description => localizer.GetText("NetworkAdapterDescription");

    public string SettingsCategory => GlanceModuleCategories.Information;

    public int Order => 36;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose() => NetworkInformation.NetworkStatusChanged -= HandleNetworkStatusChanged;

    private void HandleNetworkStatusChanged(object sender) => _ = dispatcherQueue.TryEnqueue(viewModel.Refresh);
}
