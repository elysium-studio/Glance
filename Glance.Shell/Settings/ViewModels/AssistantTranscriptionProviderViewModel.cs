using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Shell;

public sealed partial class AssistantTranscriptionProviderViewModel :
    ObservableObject
{
    public AssistantTranscriptionProviderViewModel(GlanceModuleFeedItem module, bool isInstalled)
    {
        Module = module;
        IsInstalled = isInstalled;
    }

    public string Description => Module.Description;

    public string DisplayName => Module.DisplayName;

    public string Id => Module.Id;

    public GlanceModuleFeedItem Module { get; }

    public bool CanAdd => !IsBusy && !IsInstalled;

    public bool CanRemove => !IsBusy && IsInstalled;

    public bool ShowAddAction => !IsInstalled;

    public bool ShowRemoveAction => IsInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(ShowAddAction))]
    [NotifyPropertyChangedFor(nameof(ShowRemoveAction))]
    public partial bool IsInstalled { get; set; }
}
