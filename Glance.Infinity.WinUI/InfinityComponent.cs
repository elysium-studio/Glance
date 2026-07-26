using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using System;
using System.ComponentModel;

namespace Glance.Infinity.WinUI;

public sealed partial class InfinityComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IGlanceAvailabilityComponent,
    IGlanceInteractionAwareComponent,
    IGlanceExpansionLockComponent,
    IDisposable
{
    private readonly ITextLocalizer localizer;
    private readonly InfinityViewModel viewModel;

    public InfinityComponent(InfinityViewModel viewModel, ModuleResourceTextLocalizer<InfinityModule> localizer)
    {
        this.viewModel = viewModel;
        this.localizer = localizer;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        InfinityCompactView compactView = new(viewModel);
        InfinityExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "Infinity";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 150;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public bool IsAvailable => viewModel.IsAvailable;

    public bool IsExpansionLocked => viewModel.IsEditing;

    public event EventHandler? AvailabilityChanged;

    public event EventHandler? ExpansionLockChanged;

    public void BeginInteraction() => viewModel.BeginInteraction();

    public void EndInteraction() => viewModel.EndInteraction();

    public void DismissExpansionLock() => viewModel.CancelEditing();

    public void Dispose()
    {
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(InfinityViewModel.IsAvailable))
        {
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }

        if (args.PropertyName == nameof(InfinityViewModel.IsEditing))
        {
            ExpansionLockChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
