using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Glance.Shell;

public sealed partial class AssistantSemanticResolverViewModel :
    ObservableObject,
    IGlanceViewModel
{
    private readonly IGlanceAssistantSemanticResolverService semanticResolvers;
    private int selectedIndex = -1;

    public AssistantSemanticResolverViewModel(IGlanceAssistantSemanticResolverService semanticResolvers)
    {
        this.semanticResolvers = semanticResolvers;
        semanticResolvers.PropertyChanged += HandleSemanticResolversPropertyChanged;
        Refresh();
    }

    public ObservableCollection<AssistantSemanticResolverOption> Resolvers { get; } = [];

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (!SetProperty(ref selectedIndex, value) || value < 0 || value >= Resolvers.Count)
            {
                return;
            }

            _ = semanticResolvers.SetActiveResolverAsync(Resolvers[value].Id);
        }
    }

    public void Dispose() => semanticResolvers.PropertyChanged -= HandleSemanticResolversPropertyChanged;

    private void HandleSemanticResolversPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IGlanceAssistantSemanticResolverService.Resolvers) or nameof(IGlanceAssistantSemanticResolverService.ActiveResolver))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        Resolvers.Clear();

        foreach (IGlanceAssistantSemanticResolver resolver in semanticResolvers.Resolvers)
        {
            Resolvers.Add(new AssistantSemanticResolverOption(resolver.Id, resolver.DisplayName));
        }

        selectedIndex = semanticResolvers.ActiveResolver is null
            ? -1
            : Resolvers.Select((resolver, index) => (resolver, index))
                .Where(item => string.Equals(item.resolver.Id, semanticResolvers.ActiveResolver.Id, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1).First();
        OnPropertyChanged(nameof(SelectedIndex));
    }
}

public sealed record AssistantSemanticResolverOption(string Id,
    string DisplayName);
