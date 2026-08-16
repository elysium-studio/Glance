using Elysium.UI.Controls.WinUI;
using Glance.Transcription;
using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.SpeechToText.WinUI;

public sealed partial class SpeechToTextExpandedView :
    UserControl
{
    private DesktopIsland? audioSourceExpansionIsland;

    public SpeechToTextExpandedView(SpeechToTextViewModel viewModel,
        ModuleResourceTextLocalizer<SpeechToTextModule> localizer)
    {
        ViewModel = viewModel;
        Title = localizer.GetText("ModuleDisplayName").ToUpperInvariant();
        InitializeComponent();
        Unloaded += HandleUnloaded;
    }

    public SpeechToTextViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title { get; }

    private void ToggleListening() => ViewModel.ToggleListening();

    private void CopyTranscript() => ViewModel.Copy();

    private void ClearTranscript() => ViewModel.Clear();

    private void HandleAudioSourceFlyoutOpening(object sender, object args)
    {
        AudioSourceFlyout.Items.Clear();

        foreach (AudioInputSource source in ViewModel.AudioSources)
        {
            ToggleMenuFlyoutItem item = new()
            {
                Text = source.DisplayName,
                Tag = source,
                IsChecked = string.Equals(source.Id, ViewModel.SelectedAudioSource?.Id, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += HandleAudioSourceClicked;
            AudioSourceFlyout.Items.Add(item);
        }
    }

    private void HandleAudioSourceClicked(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleMenuFlyoutItem { Tag: AudioInputSource source })
        {
            ViewModel.SelectAudioSource(source);
        }
    }

    private void HandleAudioSourceFlyoutOpened(object sender, object args) => SetExpansionLocked(true);

    private void HandleAudioSourceFlyoutClosed(object sender, object args) => ReleaseExpansionLock();

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        if (audioSourceExpansionIsland is not null)
        {
            DesktopIsland island = audioSourceExpansionIsland;
            DetachExpansionIsland();
            island.IsExpansionLocked = false;
        }
    }

    private void SetExpansionLocked(bool isLocked)
    {
        DesktopIsland? island = FindIsland();
        if (island is null)
        {
            return;
        }

        DetachExpansionIsland();
        island.IsExpansionLocked = isLocked;
        audioSourceExpansionIsland = isLocked ? island : null;
    }

    private void ReleaseExpansionLock()
    {
        DesktopIsland? island = audioSourceExpansionIsland ?? FindIsland();
        if (island is null)
        {
            return;
        }

        if (island.IsPointerWithinInteractiveRegion)
        {
            audioSourceExpansionIsland = island;
            island.PointerExited -= HandleIslandPointerExited;
            island.PointerExited += HandleIslandPointerExited;
            return;
        }

        DetachExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void HandleIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        DesktopIsland island = (DesktopIsland)sender;
        DetachExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void DetachExpansionIsland()
    {
        audioSourceExpansionIsland?.PointerExited -= HandleIslandPointerExited;
        audioSourceExpansionIsland = null;
    }

    private DesktopIsland? FindIsland()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                return island;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
