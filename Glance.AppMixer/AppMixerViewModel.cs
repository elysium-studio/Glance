using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.AppMixer;

public sealed partial class AppMixerViewModel :
    ObservableObject
{
    private static readonly TimeSpan ManualSelectionDuration = TimeSpan.FromSeconds(8);
    private readonly IAudioApplicationService service;
    private readonly ITextLocalizer localizer;
    private DateTimeOffset automaticSelectionSuppressedUntil;
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentApplicationName))]
    [NotifyPropertyChangedFor(nameof(CurrentVolumeText))]
    private AudioApplicationItemViewModel? selectedApplication;

    [ObservableProperty]
    private bool hasApplications;

    public AppMixerViewModel(IAudioApplicationService service,
        ITextLocalizer localizer)
    {
        this.service = service;
        this.localizer = localizer;
        Refresh();
    }

    public ObservableCollection<AudioApplicationItemViewModel> Applications { get; } = [];

    public string CurrentApplicationName => SelectedApplication?.DisplayName ?? localizer.GetText("NoAudioPlaying");

    public string CurrentVolumeText => SelectedApplication?.VolumeText ?? localizer.GetText("NoApplicationVolume");

    public void Refresh()
    {
        IReadOnlyList<AudioApplicationSession> sessions = service.GetApplications();
        Dictionary<string, AudioApplicationItemViewModel> existing = Applications.ToDictionary(application => application.Id, StringComparer.OrdinalIgnoreCase);
        List<AudioApplicationItemViewModel> ordered = [];

        foreach (AudioApplicationSession session in sessions)
        {
            if (existing.TryGetValue(session.Id, out AudioApplicationItemViewModel? application))
            {
                application.Update(session);
                ordered.Add(application);
            }
            else
            {
                ordered.Add(new AudioApplicationItemViewModel(session, service));
            }
        }

        Synchronize(ordered);
        HasApplications = Applications.Count > 0;
        AudioApplicationItemViewModel? current = SelectedApplication is not null
            ? Applications.FirstOrDefault(application => string.Equals(application.Id, SelectedApplication.Id, StringComparison.OrdinalIgnoreCase))
            : null;

        bool retainManualSelection = current is not null && DateTimeOffset.UtcNow < automaticSelectionSuppressedUntil;
        AudioApplicationItemViewModel? preferred = retainManualSelection
            ? current
            : Applications.FirstOrDefault(application => application.IsForeground)
                ?? Applications.OrderByDescending(application => application.PeakPercent).FirstOrDefault(application => application.PeakPercent > 0)
                ?? Applications.FirstOrDefault(application => application.IsActive)
                ?? current
                ?? Applications.FirstOrDefault();

        isRefreshing = true;
        SelectedApplication = preferred;
        isRefreshing = false;
        OnPropertyChanged(nameof(CurrentApplicationName));
        OnPropertyChanged(nameof(CurrentVolumeText));
    }

    public int CountMatchingApplications(string query) => FindApplications(query).Count;

    public bool TrySelectApplication(string query)
    {
        IReadOnlyList<AudioApplicationItemViewModel> matches = FindApplications(query);

        if (matches.Count != 1)
        {
            return false;
        }

        SelectedApplication = matches[0];
        return true;
    }

    private IReadOnlyList<AudioApplicationItemViewModel> FindApplications(string query)
    {
        AudioApplicationItemViewModel[] exactMatches = [.. Applications.Where(application =>
            string.Equals(application.Id, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(application.DisplayName, query, StringComparison.OrdinalIgnoreCase))];

        return exactMatches.Length > 0
            ? exactMatches
            : [.. Applications.Where(application => application.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    partial void OnSelectedApplicationChanged(AudioApplicationItemViewModel? value)
    {
        if (!isRefreshing && value is not null)
        {
            automaticSelectionSuppressedUntil = DateTimeOffset.UtcNow + ManualSelectionDuration;
        }

        OnPropertyChanged(nameof(CurrentApplicationName));
        OnPropertyChanged(nameof(CurrentVolumeText));
    }

    private void Synchronize(IReadOnlyList<AudioApplicationItemViewModel> ordered)
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            AudioApplicationItemViewModel application = ordered[index];

            if (index < Applications.Count && ReferenceEquals(Applications[index], application))
            {
                continue;
            }

            int currentIndex = Applications.IndexOf(application);

            if (currentIndex >= 0)
            {
                Applications.Move(currentIndex, index);
            }
            else
            {
                Applications.Insert(index, application);
            }
        }

        while (Applications.Count > ordered.Count)
        {
            Applications.RemoveAt(Applications.Count - 1);
        }
    }
}
