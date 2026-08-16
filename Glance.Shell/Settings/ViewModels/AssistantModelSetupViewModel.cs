using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Transcription;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed partial class AssistantModelSetupViewModel :
    ObservableObject,
    IGlanceViewModel,
    IDisposable
{
    private readonly ITranscriptionModelCatalog catalog;
    private readonly ITranscriptionModelSelection modelSelection;
    private readonly IDispatcher dispatcher;
    private int disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeModel))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(ShowInstallButton))]
    [NotifyPropertyChangedFor(nameof(ShowCancelButton))]
    [NotifyPropertyChangedFor(nameof(ShowRemoveButton))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    private int selectedIndex;

    public AssistantModelSetupViewModel(ITranscriptionModelCatalog catalog,
        ITranscriptionModelSelection modelSelection,
        IDispatcher dispatcher)
    {
        this.catalog = catalog;
        this.modelSelection = modelSelection;
        this.dispatcher = dispatcher;
        Models = new ObservableCollection<AssistantModelOption>(catalog.Models.Select(CreateOption));
        string selectedModelId = modelSelection.SelectedModelId ?? catalog.DefaultModelId;
        selectedIndex = catalog.Models.Select((model, index) => (model, index))
            .Where(item => string.Equals(item.model.Id, selectedModelId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        catalog.StateChanged += HandleStateChanged;
        modelSelection.SelectionChanged += HandleSelectionChanged;
        SynchronizeSelectedModel();
    }

    public string SettingsCategory => GlanceSettingsCategories.SpeechAndCommands;

    public ObservableCollection<AssistantModelOption> Models { get; }

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (!SetProperty(ref selectedIndex, value))
            {
                return;
            }

            SynchronizeSelectedModel();

            if (SelectedModel is not null)
            {
                _ = modelSelection.SelectAsync(SelectedModel.Id);
            }
        }
    }

    public AssistantModelOption? SelectedModel => SelectedIndex >= 0 && SelectedIndex < Models.Count
        ? Models[SelectedIndex]
        : null;

    public bool IsSelectedInstalled => SelectedModel is not null && catalog.IsInstalled(SelectedModel.Id);

    public bool IsSetupComplete => catalog.Models.Any(model => catalog.IsInstalled(model.Id));

    public bool CanChangeModel => true;

    public bool CanInstall => !IsBusy && SelectedModel is not null && !IsSelectedInstalled;

    public bool CanRemove => !IsBusy && IsSelectedInstalled;

    public bool ShowInstallButton => !IsBusy && !IsSelectedInstalled;

    public bool ShowCancelButton => IsBusy;

    public bool ShowRemoveButton => !IsBusy && IsSelectedInstalled;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string SelectedDescription => SelectedModel?.Description ?? string.Empty;

    public string SelectedDownloadDescription => SelectedModel is null
        ? string.Empty
        : $"One-time download · {SelectedModel.DownloadSizeText}";

    public async Task InstallAsync()
    {
        if (!CanInstall || SelectedModel is null)
        {
            return;
        }

        AssistantModelOption model = SelectedModel;
        IsBusy = true;
        ErrorMessage = null;
        Progress = 0;

        try
        {
            await catalog.InstallAsync(model.Id);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            dispatcher.Dispatch(() =>
            {
                if (Volatile.Read(ref disposed) == 0 && SelectedModel?.Id == model.Id)
                {
                    ErrorMessage = exception.Message;
                }
            });
        }
        finally
        {
            dispatcher.Dispatch(() =>
            {
                if (Volatile.Read(ref disposed) == 0 && SelectedModel?.Id == model.Id)
                {
                    SynchronizeSelectedModel();
                }
            });
        }
    }

    public void Cancel()
    {
        if (SelectedModel is not null)
        {
            _ = catalog.CancelInstall(SelectedModel.Id);
        }
    }

    public async Task RemoveAsync()
    {
        if (!CanRemove || SelectedModel is null)
        {
            return;
        }

        AssistantModelOption model = SelectedModel;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await catalog.RemoveAsync(model.Id);
        }
        catch (Exception exception)
        {
            dispatcher.Dispatch(() =>
            {
                if (Volatile.Read(ref disposed) == 0 && SelectedModel?.Id == model.Id)
                {
                    ErrorMessage = exception.Message;
                }
            });
        }
        finally
        {
            dispatcher.Dispatch(() =>
            {
                if (Volatile.Read(ref disposed) == 0 && SelectedModel?.Id == model.Id)
                {
                    SynchronizeSelectedModel();
                }
            });
        }
    }

    public void Dispose()
    {
        _ = Interlocked.Exchange(ref disposed, 1);
        catalog.StateChanged -= HandleStateChanged;
        modelSelection.SelectionChanged -= HandleSelectionChanged;
    }

    private void HandleStateChanged(object? sender, EventArgs args) => dispatcher.Dispatch(() =>
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            SynchronizeModels();
            SynchronizeSelectedModel();
        }
    });

    private void HandleSelectionChanged(object? sender, EventArgs args) => dispatcher.Dispatch(() =>
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        int index = Models.Select((model, index) => (model, index))
            .Where(item => string.Equals(item.model.Id, modelSelection.SelectedModelId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();

        if (selectedIndex != index)
        {
            selectedIndex = index;
            OnPropertyChanged(nameof(SelectedIndex));
        }

        SynchronizeSelectedModel();
    });

    private void SynchronizeModels()
    {
        IReadOnlyList<TranscriptionModel> current = catalog.Models;

        if (Models.Select(model => model.Id).SequenceEqual(current.Select(model => model.Id), StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        string selectedModelId = modelSelection.SelectedModelId ?? SelectedModel?.Id ?? catalog.DefaultModelId;
        Models.Clear();

        foreach (TranscriptionModel model in current)
        {
            Models.Add(CreateOption(model));
        }

        selectedIndex = Models.Select((model, index) => (model, index))
            .Where(item => string.Equals(item.model.Id, selectedModelId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(Models.Count > 0 ? 0 : -1)
            .First();
        OnPropertyChanged(nameof(SelectedIndex));
    }

    private void SynchronizeSelectedModel()
    {
        TranscriptionModelDownload? download = SelectedModel is null
            ? null
            : catalog.GetDownload(SelectedModel.Id);
        IsBusy = download?.IsActive == true;
        Progress = download is null
            ? IsSelectedInstalled ? 100 : 0
            : download.Progress * 100;
        ErrorMessage = download?.Status == TranscriptionModelDownloadStatus.Failed
            ? download.ErrorMessage
            : null;
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(IsSelectedInstalled));
        OnPropertyChanged(nameof(IsSetupComplete));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(ShowInstallButton));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(ShowRemoveButton));
        OnPropertyChanged(nameof(SelectedDescription));
        OnPropertyChanged(nameof(SelectedDownloadDescription));
    }

    private static AssistantModelOption CreateOption(TranscriptionModel model) => new(model.Id,
        model.DisplayName,
        model.Description,
        FormatSize(model.DownloadSize),
        model.IsRecommended,
        model.ProviderDisplayName);

    private static string FormatSize(long size)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        const double megabyte = 1024d * 1024d;
        return size >= gigabyte
            ? $"{size / gigabyte:0.0} GB"
            : $"{size / megabyte:0} MB";
    }
}

public sealed record AssistantModelOption(string Id,
    string DisplayName,
    string Description,
    string DownloadSizeText,
    bool IsRecommended,
    string? ProviderDisplayName)
{
    public string DisplayLabel
    {
        get
        {
            string label = string.IsNullOrWhiteSpace(ProviderDisplayName)
                ? DisplayName
                : $"{DisplayName} · {ProviderDisplayName}";
            return IsRecommended ? $"{label} · Recommended" : label;
        }
    }
}
