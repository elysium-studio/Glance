using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Glance.Shell;

public sealed partial class AssistantProviderViewModel :
    ObservableObject,
    IGlanceViewModel
{
    private readonly IGlanceAssistantService assistant;
    private int selectedIndex = -1;

    public AssistantProviderViewModel(IGlanceAssistantService assistant)
    {
        this.assistant = assistant;
        assistant.PropertyChanged += HandleAssistantPropertyChanged;
        Refresh();
    }

    public ObservableCollection<AssistantProviderOption> Providers { get; } = [];

    public string SettingsCategory => GlanceSettingsCategories.SpeechAndCommands;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (!SetProperty(ref selectedIndex, value) || value < 0 || value >= Providers.Count)
            {
                return;
            }

            _ = assistant.SetActiveProviderAsync(Providers[value].Id);
        }
    }

    public void Dispose() => assistant.PropertyChanged -= HandleAssistantPropertyChanged;

    private void HandleAssistantPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IGlanceAssistantService.Providers) or nameof(IGlanceAssistantService.ActiveProvider))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        Providers.Clear();

        foreach (IGlanceAssistantProvider provider in assistant.Providers)
        {
            Providers.Add(new AssistantProviderOption(provider.Id, provider.DisplayName));
        }

        selectedIndex = assistant.ActiveProvider is null
            ? -1
            : Providers.Select((provider, index) => (provider, index))
                .Where(item => string.Equals(item.provider.Id, assistant.ActiveProvider.Id, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1).First();
        OnPropertyChanged(nameof(SelectedIndex));
    }
}

public sealed record AssistantProviderOption(string Id,
    string DisplayName);
