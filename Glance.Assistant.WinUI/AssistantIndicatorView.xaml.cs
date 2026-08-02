using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Assistant.WinUI;

public sealed partial class AssistantIndicatorView :
    UserControl,
    IGlanceAssistantConnectedAnimationView
{
    private readonly IGlanceAssistantService assistant;
    private readonly IGlanceAssistantProvider provider;
    private readonly bool isCompact;
    private readonly CompositionActivityPulse? activityPulse;
    private bool isPreparingAnimationRunning;

    public AssistantIndicatorView(IGlanceAssistantProvider provider,
        IGlanceAssistantService assistant,
        bool isCompact)
    {
        this.provider = provider;
        this.assistant = assistant;
        this.isCompact = isCompact;
        InitializeComponent();
        Width = isCompact ? 28 : 24;
        Height = isCompact ? 28 : 24;
        CompactSurface.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        ExpandedToggleButton.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        activityPulse = isCompact ? new(this,
            PulseRing,
            provider,
            nameof(IGlanceAssistantProvider.State),
            IsAssistantActive) : null;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        UpdateState();
    }

    public object ConnectedAnimationElement => isCompact ? CompactToggleButton : ExpandedToggleButton;

    private void HandleClick(object sender, RoutedEventArgs args)
    {
        CompactToggleButton.IsEnabled = false;
        ExpandedToggleButton.IsEnabled = false;
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
            DispatcherQueue.TryEnqueue(() =>
            {
                CompactToggleButton.IsEnabled = true;
                ExpandedToggleButton.IsEnabled = true;
            });
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
        StopPreparingAnimation();
    }

    private void HandleProviderPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(UpdateState);

    private void UpdateState()
    {
        (string glyph, string label) = provider.State switch
        {
            GlanceAssistantState.Disabled => ("\uEC54", "Voice assistant is off"),
            GlanceAssistantState.Preparing => ("\uE895", "Preparing voice assistant"),
            GlanceAssistantState.ListeningForWakeWord => ("\uE720", "Voice assistant is ready — say “Hey Glance”"),
            GlanceAssistantState.ListeningForCommand => ("\uE720", "Listening for your command"),
            GlanceAssistantState.ProcessingCommand => ("\uE720", "Processing your command"),
            GlanceAssistantState.Error => ("\uE783", "Voice assistant is unavailable"),
            _ => ("\uEC54", "Voice assistant is off")
        };
        CompactGlyph.Glyph = glyph;
        CompactToggleButton.Opacity = 1;
        ExpandedGlyph.Glyph = glyph;
        ExpandedToggleButton.Opacity = 1;
        AutomationProperties.SetName(CompactToggleButton, label);
        AutomationProperties.SetName(ExpandedToggleButton, label);
        ToolTipService.SetToolTip(CompactToggleButton, label);
        ToolTipService.SetToolTip(ExpandedToggleButton, label);

        if (provider.State == GlanceAssistantState.Preparing)
        {
            StartPreparingAnimation();
        }
        else
        {
            StopPreparingAnimation();
        }

        activityPulse?.Refresh();
    }

    private void StartPreparingAnimation()
    {
        FontIcon glyph = isCompact ? CompactGlyph : ExpandedGlyph;

        if (isPreparingAnimationRunning || !glyph.IsLoaded)
        {
            return;
        }

        isPreparingAnimationRunning = true;
        Visual visual = ElementCompositionPreview.GetElementVisual(glyph);
        visual.CenterPoint = new Vector3((float)glyph.ActualWidth / 2, (float)glyph.ActualHeight / 2, 0);
        ScalarKeyFrameAnimation rotation = visual.Compositor.CreateScalarKeyFrameAnimation();
        rotation.InsertKeyFrame(0, 0);
        rotation.InsertKeyFrame(1, 360);
        rotation.Duration = TimeSpan.FromMilliseconds(1000);
        rotation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.RotationAngleInDegrees), rotation);
    }

    private void StopPreparingAnimation()
    {
        isPreparingAnimationRunning = false;
        Visual compactVisual = ElementCompositionPreview.GetElementVisual(CompactGlyph);
        compactVisual.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        compactVisual.RotationAngleInDegrees = 0;
        Visual expandedVisual = ElementCompositionPreview.GetElementVisual(ExpandedGlyph);
        expandedVisual.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        expandedVisual.RotationAngleInDegrees = 0;
    }

    private bool IsAssistantActive() =>
        provider.State is GlanceAssistantState.ListeningForWakeWord or
            GlanceAssistantState.ListeningForCommand or
            GlanceAssistantState.ProcessingCommand;
}
