using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Glance.Shell;

public sealed class GlanceAssistantService :
    ObservableObject,
    IGlanceAssistantService,
    IRecipient<OptionsChangedEventArgs<GlanceSettings>>
{
    private readonly ILogger<GlanceAssistantService> logger;
    private readonly List<IGlanceAssistantProvider> providers = [];
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> settingsWriter;
    private IGlanceAssistantProvider? activeProvider;
    private bool isEnabled;

    public GlanceAssistantService(GlanceSettings settings,
        IWritableOptions<GlanceSettings> settingsWriter,
        IMessenger messenger,
        ILogger<GlanceAssistantService> logger)
    {
        this.settings = settings;
        this.settingsWriter = settingsWriter;
        this.logger = logger;
        isEnabled = settings.IsAssistantEnabled;
        messenger.Register(this);
    }

    public event EventHandler? WakeWordDetected;

    public IReadOnlyList<IGlanceAssistantProvider> Providers => providers;

    public IGlanceAssistantProvider? ActiveProvider
    {
        get => activeProvider;
        private set
        {
            if (ReferenceEquals(activeProvider, value))
            {
                return;
            }

            if (activeProvider is not null)
            {
                activeProvider.PropertyChanged -= HandleProviderPropertyChanged;
            }

            activeProvider = value;

            if (activeProvider is not null)
            {
                activeProvider.PropertyChanged += HandleProviderPropertyChanged;
            }

            OnPropertyChanged();
            NotifyPresentationChanged();
        }
    }

    public bool IsAvailable => ActiveProvider is not null;

    public bool IsEnabled
    {
        get => isEnabled;
        private set => SetProperty(ref isEnabled, value);
    }

    public bool IsOverlayVisible => ActiveProvider?.State is GlanceAssistantState.ListeningForCommand or GlanceAssistantState.ProcessingCommand;

    public object? CompactIndicatorContent => ActiveProvider?.CompactIndicatorContent;

    public object? ExpandedIndicatorContent => ActiveProvider?.ExpandedIndicatorContent;

    public object? OverlayContent => ActiveProvider?.OverlayContent;

    public void Register(IEnumerable<IGlanceAssistantProvider> registrations)
    {
        foreach (IGlanceAssistantProvider provider in registrations)
        {
            if (providers.Any(candidate => string.Equals(candidate.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            providers.Add(provider);
        }

        OnPropertyChanged(nameof(Providers));

        if (ActiveProvider is null)
        {
            ActiveProvider = providers.FirstOrDefault(provider => string.Equals(provider.Id, settings.AssistantProviderId, StringComparison.OrdinalIgnoreCase)) ?? providers.FirstOrDefault();
        }

        if (ActiveProvider is not null && IsEnabled)
        {
            _ = EnableProviderAsync(ActiveProvider);
        }
    }

    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (IsEnabled == isEnabled)
        {
            return;
        }

        IsEnabled = isEnabled;
        await settingsWriter.WriteAsync(options => options.IsAssistantEnabled = isEnabled);

        if (ActiveProvider is not null)
        {
            await ActiveProvider.SetEnabledAsync(isEnabled, cancellationToken);
        }

        NotifyPresentationChanged();
    }

    public async Task SetActiveProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        IGlanceAssistantProvider? nextProvider = providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));

        if (nextProvider is null || ReferenceEquals(nextProvider, ActiveProvider))
        {
            return;
        }

        IGlanceAssistantProvider? previousProvider = ActiveProvider;

        if (previousProvider is not null)
        {
            await previousProvider.SetEnabledAsync(false, cancellationToken);
        }

        ActiveProvider = nextProvider;
        await settingsWriter.WriteAsync(options => options.AssistantProviderId = nextProvider.Id);

        if (IsEnabled)
        {
            await nextProvider.SetEnabledAsync(true, cancellationToken);
        }
    }

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message)
    {
        if (IsEnabled == message.Options.IsAssistantEnabled)
        {
            return;
        }

        IsEnabled = message.Options.IsAssistantEnabled;

        if (ActiveProvider is not null)
        {
            _ = ApplyEnabledStateAsync(ActiveProvider, IsEnabled);
        }

        NotifyPresentationChanged();
    }

    private async Task EnableProviderAsync(IGlanceAssistantProvider provider)
    {
        try
        {
            await provider.SetEnabledAsync(true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to start assistant provider {AssistantProvider}", provider.Id);
        }
    }

    private async Task ApplyEnabledStateAsync(IGlanceAssistantProvider provider, bool isEnabled)
    {
        try
        {
            await provider.SetEnabledAsync(isEnabled);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update assistant provider {AssistantProvider}", provider.Id);
        }
    }

    private void HandleProviderPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, ActiveProvider))
        {
            return;
        }

        NotifyPresentationChanged();

        if (args.PropertyName == nameof(IGlanceAssistantProvider.State) && ActiveProvider?.State == GlanceAssistantState.ListeningForCommand)
        {
            WakeWordDetected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(CompactIndicatorContent));
        OnPropertyChanged(nameof(ExpandedIndicatorContent));
        OnPropertyChanged(nameof(OverlayContent));
    }
}
