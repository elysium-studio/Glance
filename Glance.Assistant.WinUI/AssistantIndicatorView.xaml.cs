using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Assistant.WinUI;

public sealed partial class AssistantIndicatorView :
    UserControl
{
    private readonly IGlanceAssistantService assistant;
    private readonly MicrosoftOfflineAssistantProvider provider;
    private bool isPulseRunning;

    public AssistantIndicatorView(MicrosoftOfflineAssistantProvider provider,
        IGlanceAssistantService assistant)
    {
        this.provider = provider;
        this.assistant = assistant;
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        UpdateState();
    }

    private void HandleClick(object sender, RoutedEventArgs args)
    {
        ToggleButton.IsEnabled = false;
        _ = ToggleAssistantAsync();
    }

    private async Task ToggleAssistantAsync()
    {
        try
        {
            await assistant.SetEnabledAsync(!assistant.IsEnabled);
        }
        catch (Exception)
        {
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() => ToggleButton.IsEnabled = true);
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        provider.PropertyChanged += HandleProviderPropertyChanged;
        UpdateState();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        provider.PropertyChanged -= HandleProviderPropertyChanged;
        StopPulse();
    }

    private void HandleProviderPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(UpdateState);

    private void UpdateState()
    {
        ToggleButton.Content = provider.State == GlanceAssistantState.Disabled ? "\uE198" : "\uE720";
        ToggleButton.Opacity = provider.State == GlanceAssistantState.Disabled ? 0.55 : 1;

        if (provider.State is GlanceAssistantState.Preparing or GlanceAssistantState.ListeningForWakeWord or GlanceAssistantState.ListeningForCommand or GlanceAssistantState.ProcessingCommand)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    private void StartPulse()
    {
        if (isPulseRunning || !ListeningRing.IsLoaded)
        {
            return;
        }

        isPulseRunning = true;
        Visual visual = ElementCompositionPreview.GetElementVisual(ListeningRing);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3(16, 16, 0);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.7f);
        opacity.InsertKeyFrame(0.7f, 0.12f);
        opacity.InsertKeyFrame(1, 0f);
        opacity.Duration = TimeSpan.FromMilliseconds(1600);
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.72f, 0.72f, 1));
        scale.InsertKeyFrame(1, new Vector3(1.18f, 1.18f, 1));
        scale.Duration = opacity.Duration;
        scale.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation(nameof(Visual.Opacity), opacity);
        visual.StartAnimation(nameof(Visual.Scale), scale);
    }

    private void StopPulse()
    {
        isPulseRunning = false;
        Visual visual = ElementCompositionPreview.GetElementVisual(ListeningRing);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.StopAnimation(nameof(Visual.Scale));
        visual.Opacity = 0;
        visual.Scale = Vector3.One;
    }
}
