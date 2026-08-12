using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.Torrents;

public sealed partial class TorrentItemViewModel : ObservableObject
{
    [ObservableProperty] private string id = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private TorrentDownloadState state;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string progressText = "0%";
    [ObservableProperty] private string downloadSpeedText = "0 B/s";
    [ObservableProperty] private string uploadSpeedText = "0 B/s";
    [ObservableProperty] private string peerText = "0 peers";
    [ObservableProperty] private string stateText = string.Empty;
    [ObservableProperty] private long downloadSpeed;
    [ObservableProperty] private bool canPause;
    [ObservableProperty] private bool canResume;

    public void Update(TorrentTransferSnapshot snapshot)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        State = snapshot.State;
        Progress = Math.Clamp(snapshot.Progress, 0, 100);
        ProgressText = $"{Progress:0}%";
        DownloadSpeed = Math.Max(0, snapshot.DownloadSpeed);
        DownloadSpeedText = FormatRate(DownloadSpeed);
        UploadSpeedText = FormatRate(snapshot.UploadSpeed);
        PeerText = $"{snapshot.PeerCount} {(snapshot.PeerCount == 1 ? "peer" : "peers")}";
        StateText = snapshot.ErrorMessage ?? FormatState(snapshot.State);
        CanPause = snapshot.State is TorrentDownloadState.Downloading or TorrentDownloadState.Seeding or TorrentDownloadState.Checking;
        CanResume = snapshot.State is TorrentDownloadState.Paused or TorrentDownloadState.Stopped;
    }

    public static string FormatRate(long bytesPerSecond) => Math.Max(0, bytesPerSecond) switch
    {
        >= 1024L * 1024 * 1024 => $"{bytesPerSecond / (1024d * 1024 * 1024):0.0} GB/s",
        >= 1024L * 1024 => $"{bytesPerSecond / (1024d * 1024):0.0} MB/s",
        >= 1024 => $"{bytesPerSecond / 1024d:0} KB/s",
        _ => $"{bytesPerSecond} B/s"
    };

    private static string FormatState(TorrentDownloadState value) => value switch
    {
        TorrentDownloadState.RetrievingMetadata => "Retrieving metadata",
        TorrentDownloadState.Checking => "Checking files",
        TorrentDownloadState.Downloading => "Downloading",
        TorrentDownloadState.Paused => "Paused",
        TorrentDownloadState.Seeding => "Seeding",
        TorrentDownloadState.Completed => "Complete",
        TorrentDownloadState.Stopped => "Stopped",
        TorrentDownloadState.Error => "Error",
        _ => "Queued"
    };
}

public sealed partial class TorrentsViewModel : ObservableObject
{
    [ObservableProperty] private int activeCount;
    [ObservableProperty] private string compactSummary = "No active downloads";
    [ObservableProperty] private string totalDownloadSpeedText = "0 B/s";
    [ObservableProperty] private bool hasTorrents;

    public ObservableCollection<TorrentItemViewModel> Torrents { get; } = [];

    public void Update(TorrentTransferSnapshot snapshot)
    {
        TorrentItemViewModel? item = Torrents.FirstOrDefault(item => string.Equals(item.Id, snapshot.Id, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            item = new TorrentItemViewModel();
            Torrents.Add(item);
        }

        item.Update(snapshot);
        RefreshAggregate();
    }

    public void Remove(string torrentId)
    {
        TorrentItemViewModel? item = Torrents.FirstOrDefault(item => string.Equals(item.Id, torrentId, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            _ = Torrents.Remove(item);
            RefreshAggregate();
        }
    }

    private void RefreshAggregate()
    {
        ActiveCount = Torrents.Count(item => item.State is TorrentDownloadState.Downloading or TorrentDownloadState.Checking or TorrentDownloadState.RetrievingMetadata);
        long aggregateSpeed = Torrents.Where(item => item.State == TorrentDownloadState.Downloading)
            .Sum(item => item.DownloadSpeed);
        TotalDownloadSpeedText = TorrentItemViewModel.FormatRate(aggregateSpeed);
        CompactSummary = ActiveCount == 0 ? "No active downloads" : $"{ActiveCount} active · {TotalDownloadSpeedText}";
        HasTorrents = Torrents.Count > 0;
    }

}
