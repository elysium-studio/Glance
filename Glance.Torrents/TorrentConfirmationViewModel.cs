using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.Torrents;

public sealed partial class TorrentFileSelectionViewModel(TorrentMetadataFile file) : ObservableObject
{
    public string Path { get; } = file.Path;
    public long Size { get; } = file.Size;
    [ObservableProperty] private bool isSelected = file.IsSelected;
}

public sealed partial class TorrentConfirmationViewModel : ObservableObject
{
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string downloadPath = string.Empty;
    [ObservableProperty] private long totalSize;
    [ObservableProperty] private long selectedSize;

    public ObservableCollection<TorrentFileSelectionViewModel> Files { get; } = [];
    public ObservableCollection<string> Trackers { get; } = [];

    public void Load(TorrentMetadataSession session)
    {
        Name = session.Name;
        DownloadPath = session.DownloadPath;
        TotalSize = session.TotalSize;
        Files.Clear();
        Trackers.Clear();
        foreach (TorrentMetadataFile file in session.Files)
        {
            TorrentFileSelectionViewModel item = new(file);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(item.IsSelected)) RefreshSelectedSize();
            };
            Files.Add(item);
        }
        foreach (string tracker in session.Trackers) Trackers.Add(tracker);
        RefreshSelectedSize();
        IsLoading = false;
        ErrorMessage = null;
    }

    public IReadOnlyList<string> GetSelectedFiles() => Files.Where(file => file.IsSelected).Select(file => file.Path).ToArray();

    private void RefreshSelectedSize() => SelectedSize = Files.Where(file => file.IsSelected).Sum(file => file.Size);
}
