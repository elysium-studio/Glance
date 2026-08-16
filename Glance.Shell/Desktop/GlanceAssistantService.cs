using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Transcription;
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
    private readonly ITranscriptionModelCatalog modelCatalog;
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
        ITranscriptionModelCatalog modelCatalog,
        ILogger<GlanceAssistantService> logger)
    {
        this.settings = settings;
        this.settingsWriter = settingsWriter;
        this.dispatcher = dispatcher;
        this.actionService = actionService;
        this.modelCatalog = modelCatalog;
        this.logger = logger;
        isEnabled = settings.IsAssistantEnabled;
        actionService.PresentationRequested += HandleActionPresentationRequested;
        modelCatalog.StateChanged += HandleModelStateChanged;
        messenger.Register(this);

        if (settings.IsAssistantEnabled)
        {
            _ = ValidateModelAvailabilityAsync();
        }
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

        ActiveProvider ??= providers.FirstOrDefault();

        if (!ReferenceEquals(previousProvider, ActiveProvider) && ActiveProvider is not null && IsEnabled)
        {
            _ = EnableProviderAsync(ActiveProvider);
        }
    }

    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        if (isEnabled && !CanEnable)
        {
            return;
        }

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

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) => dispatcher.Dispatch(() => ApplySettings(message.Options));

    public void Dispose()
    {
        actionService.PresentationRequested -= HandleActionPresentationRequested;
        modelCatalog.StateChanged -= HandleModelStateChanged;
    }

    private void ApplySettings(GlanceSettings options)
    {
        if (options.IsAssistantEnabled && !CanEnable)
        {
            options.IsAssistantEnabled = false;
            _ = settingsWriter.WriteAsync(settings => settings.IsAssistantEnabled = false);
        }

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

    private void HandleModelStateChanged(object? sender, EventArgs args) => dispatcher.Dispatch(() =>
    {
        OnPropertyChanged(nameof(CanEnable));
        _ = ValidateModelAvailabilityAsync();
    });

    private async Task ValidateModelAvailabilityAsync()
    {
        TranscriptionModel[] models = [.. modelCatalog.Models];

        if (models.Length == 0 || !IsEnabled)
        {
            return;
        }

        bool isAvailable = false;

        foreach (TranscriptionModel model in models)
        {
            try
            {
                if (await modelCatalog.GetStateAsync(model.Id) == TranscriptionModelState.Installed)
                {
                    isAvailable = true;
                    break;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to read transcription model state for {TranscriptionModelId}", model.Id);
            }
        }

        if (!isAvailable)
        {
            if (!models.Select(model => model.Id).SequenceEqual(modelCatalog.Models.Select(model => model.Id), StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            dispatcher.Dispatch(() => _ = SetEnabledAsync(false));
        }
    }

    public bool CanEnable => modelCatalog.Models.Any(model => modelCatalog.IsInstalled(model.Id));

    public async Task UnregisterAsync(IEnumerable<IGlanceAssistantProvider> registrations)
    {
        HashSet<IGlanceAssistantProvider> removals = [.. registrations];
        IGlanceAssistantProvider? removedActiveProvider = ActiveProvider is not null && removals.Contains(ActiveProvider)
            ? ActiveProvider
            : null;

        if (removedActiveProvider is not null)
        {
            await removedActiveProvider.SetEnabledAsync(false);
        }

        _ = providers.RemoveAll(removals.Contains);

        if (removedActiveProvider is not null)
        {
            ActiveProvider = providers.FirstOrDefault();

            if (ActiveProvider is not null && IsEnabled)
            {
                await EnableProviderAsync(ActiveProvider);
            }
        }

        OnPropertyChanged(nameof(Providers));
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
