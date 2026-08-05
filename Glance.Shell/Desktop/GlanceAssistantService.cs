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
    IRecipient<OptionsChangedEventArgs<GlanceSettings>>,
    IDisposable
{
    private readonly IGlanceActionService actionService;
    private readonly ILogger<GlanceAssistantService> logger;
    private readonly List<IGlanceAssistantProvider> providers = [];
    private readonly IDispatcher dispatcher;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> settingsWriter;
    private bool isEnabled;
    private int presentationNotificationPending;

    public GlanceAssistantService(GlanceSettings settings,
        IWritableOptions<GlanceSettings> settingsWriter,
        IMessenger messenger,
        IDispatcher dispatcher,
        IGlanceActionService actionService,
        ILogger<GlanceAssistantService> logger)
    {
        this.settings = settings;
        this.settingsWriter = settingsWriter;
        this.dispatcher = dispatcher;
        this.actionService = actionService;
        this.logger = logger;
        isEnabled = settings.IsAssistantEnabled;
        actionService.PresentationRequested += HandleActionPresentationRequested;
        messenger.Register(this);
    }

    public event EventHandler? WakeWordDetected;

    public IReadOnlyList<IGlanceAssistantProvider> Providers => providers;

    public IGlanceAssistantProvider? ActiveProvider
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.PropertyChanged -= HandleProviderPropertyChanged;

            field = value;
            IsResultPresentationActive = false;

            field?.PropertyChanged += HandleProviderPropertyChanged;

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

    public bool IsOverlayVisible => !IsResultPresentationActive &&
        ActiveProvider?.State is GlanceAssistantState.ListeningForCommand or GlanceAssistantState.ProcessingCommand;

    public bool IsResultPresentationActive { get; private set; }

    public object? CompactIndicatorContent => ActiveProvider?.CompactIndicatorContent;

    public object? ExpandedIndicatorContent => ActiveProvider?.ExpandedIndicatorContent;

    public object? OverlayContent => ActiveProvider?.OverlayContent;

    public void Register(IEnumerable<IGlanceAssistantProvider> registrations)
    {
        IGlanceAssistantProvider? previousProvider = ActiveProvider;

        foreach (IGlanceAssistantProvider provider in registrations)
        {
            if (providers.Any(candidate => string.Equals(candidate.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            providers.Add(provider);
        }

        OnPropertyChanged(nameof(Providers));

        ActiveProvider ??= providers.FirstOrDefault(provider => string.Equals(provider.Id, settings.AssistantProviderId, StringComparison.OrdinalIgnoreCase)) ?? providers.FirstOrDefault();

        if (!ReferenceEquals(previousProvider, ActiveProvider) && ActiveProvider is not null && IsEnabled)
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

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) => dispatcher.Dispatch(() => ApplySettings(message.Options));

    public void Dispose() => actionService.PresentationRequested -= HandleActionPresentationRequested;

    private void ApplySettings(GlanceSettings options)
    {
        if (IsEnabled == options.IsAssistantEnabled)
        {
            return;
        }

        IsEnabled = options.IsAssistantEnabled;

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

        if (args.PropertyName == nameof(IGlanceAssistantProvider.State))
        {
            if (ActiveProvider?.State == GlanceAssistantState.ListeningForCommand)
            {
                IsResultPresentationActive = false;
                WakeWordDetected?.Invoke(this, EventArgs.Empty);
            }
            else if (ActiveProvider?.State is GlanceAssistantState.ListeningForWakeWord or GlanceAssistantState.Disabled or GlanceAssistantState.Error)
            {
                IsResultPresentationActive = false;
            }
        }

        NotifyPresentationChanged();
    }

    private void HandleActionPresentationRequested(object? sender, GlanceActionPresentationRequestedEventArgs args) => dispatcher.Dispatch(() => dispatcher.Dispatch(() =>
                                                                                                                            {
                                                                                                                                if (ActiveProvider?.State is not (GlanceAssistantState.ListeningForCommand or GlanceAssistantState.ProcessingCommand))
                                                                                                                                {
                                                                                                                                    return;
                                                                                                                                }

                                                                                                                                IsResultPresentationActive = true;
                                                                                                                                NotifyPresentationChanged();
                                                                                                                            }));

    private void NotifyPresentationChanged()
    {
        if (Interlocked.Exchange(ref presentationNotificationPending, 1) != 0)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            _ = Interlocked.Exchange(ref presentationNotificationPending, 0);
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(IsOverlayVisible));
            OnPropertyChanged(nameof(IsResultPresentationActive));
            OnPropertyChanged(nameof(CompactIndicatorContent));
            OnPropertyChanged(nameof(ExpandedIndicatorContent));
            OnPropertyChanged(nameof(OverlayContent));
        });
    }
}
