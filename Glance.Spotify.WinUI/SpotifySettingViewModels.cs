using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Spotify;
using Glance.UI.WinUI;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyClientIdSettingViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    SpotifySettings settings,
    IWritableOptions<SpotifySettings> writer,
    ISpotifySetupService setupService,
    ModuleResourceTextLocalizer<SpotifyModule> localizer) :
    ModuleSettingViewModel<SpotifySettings, string>(provider,
        factory,
        messenger,
        disposer,
        dispatcher,
        settings,
        writer,
        "Spotify",
        10,
        config => config.ClientId,
        (config, value) => config.ClientId = value?.Trim() ?? string.Empty)
{
    [ObservableProperty]
    public partial bool HasValidationError { get; private set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    public string RedirectUri => setupService.RedirectUri;

    public override void Activated()
    {
        base.Activated();
        Validate(Value);
    }

    public Task OpenDashboardAsync() => setupService.OpenDashboardAsync();

    public Task<bool> CopyRedirectUriAsync() => setupService.CopyRedirectUriAsync();

    protected override void ValueChanged(string? value)
    {
        base.ValueChanged(value);
        Validate(value);
    }

    private void Validate(string? value)
    {
        bool empty = string.IsNullOrWhiteSpace(value);
        HasValidationError = !empty && !SpotifyClientIdValidator.IsValid(value);
        ValidationMessage = HasValidationError
            ? localizer.GetText("InvalidClientId")
            : string.Empty;
    }
}

public sealed partial class SpotifyConnectionSettingViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel,
    IRecipient<OptionsChangedEventArgs<SpotifySettings>>
{
    private readonly ISpotifyConnectionService connectionService;
    private readonly ISpotifyProfileService profileService;
    private readonly ModuleResourceTextLocalizer<SpotifyModule> localizer;
    private readonly IMessenger messenger;
    private readonly IDispatcher dispatcher;
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken token;
    private SpotifySettings settings;
    private string currentClientId;
    private bool active;
    private int disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeConnection))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConfigurationError))]
    [NotifyPropertyChangedFor(nameof(CanChangeConnection))]
    private bool isConfigured;

    [ObservableProperty]
    private string statusText;

    [ObservableProperty]
    private string buttonText;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public SpotifyConnectionSettingViewModel(SpotifySettings settings,
        ISpotifyConnectionService connectionService,
        ISpotifyProfileService profileService,
        ModuleResourceTextLocalizer<SpotifyModule> localizer,
        IMessenger messenger,
        IDispatcher dispatcher)
    {
        this.settings = settings;
        this.connectionService = connectionService;
        this.profileService = profileService;
        this.localizer = localizer;
        this.messenger = messenger;
        this.dispatcher = dispatcher;
        token = cancellation.Token;
        currentClientId = settings.ClientId;
        statusText = localizer.GetText("SpotifyDisconnected");
        buttonText = localizer.GetText("ConnectSpotify");
        ApplyConfiguration();
    }

    public string ModuleId => "Spotify";

    public int Order => 20;

    public bool CanChangeConnection => IsConfigured && !IsBusy;

    public bool HasConfigurationError => !IsConfigured;

    public string ConfigurationErrorMessage => localizer.GetText("EnterClientIdFirst");

    public void Activate()
    {
        if (active || Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        active = true;
        messenger.Register(this);
        connectionService.StateChanged += HandleConnectionStateChanged;
        _ = RestoreAsync();
    }

    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        connectionService.StateChanged -= HandleConnectionStateChanged;
        messenger.UnregisterAll(this);
    }

    public async Task ChangeConnectionAsync()
    {
        if (!CanChangeConnection)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            if (connectionService.State == SpotifyConnectionState.Connected)
            {
                await connectionService.DisconnectAsync(token);
            }
            else
            {
                SpotifyConnectionResult result = await connectionService.ConnectAsync(settings.ClientId,
                    token);

                if (!result.Succeeded)
                {
                    ShowError(result.ErrorMessage ?? localizer.GetText("SpotifyConnectionFailed"));
                }
            }

            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Receive(OptionsChangedEventArgs<SpotifySettings> message) =>
        dispatcher.Dispatch(() =>
        {
            string clientId = message.Options.ClientId;
            bool configurationChanged = !string.Equals(currentClientId, clientId, StringComparison.Ordinal);
            settings = message.Options;
            currentClientId = clientId;
            ApplyConfiguration();

            if (configurationChanged)
            {
                _ = DisconnectForConfigurationChangeAsync();
            }
        });

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Deactivate();
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task RestoreAsync()
    {
        if (!IsConfigured)
        {
            await RefreshAsync();
            return;
        }

        IsBusy = true;

        try
        {
            _ = await connectionService.RestoreAsync(settings.ClientId, token);
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectForConfigurationChangeAsync()
    {
        try
        {
            await connectionService.DisconnectAsync(token);
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async Task RefreshAsync()
    {
        SpotifyConnectionState state = connectionService.State;
        ButtonText = state == SpotifyConnectionState.Connected
            ? localizer.GetText("DisconnectSpotify")
            : localizer.GetText("ConnectSpotify");

        if (!IsConfigured)
        {
            StatusText = localizer.GetText("SpotifyDisconnected");
            return;
        }

        if (state == SpotifyConnectionState.Connected)
        {
            try
            {
                SpotifyAccount? account = await profileService.GetCurrentAccountAsync(token);
                StatusText = account is null
                    ? localizer.GetText("SpotifyConnected")
                    : string.Format(localizer.GetText("SpotifyConnectedAs"), account.DisplayName);
                ClearError();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                StatusText = localizer.GetText("SpotifyConnected");
                ShowError(exception.Message);
            }

            return;
        }

        StatusText = state == SpotifyConnectionState.Connecting
            ? localizer.GetText("Connecting")
            : localizer.GetText("SpotifyDisconnected");
    }

    private void HandleConnectionStateChanged(object? sender,
        SpotifyConnectionStateChangedEventArgs args) => dispatcher.Dispatch(() =>
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(args.ErrorMessage))
            {
                ShowError(args.ErrorMessage);
            }

            _ = RefreshAsync();
        });

    private void ApplyConfiguration()
    {
        IsConfigured = SpotifyClientIdValidator.IsValid(settings.ClientId);
        ButtonText = connectionService.State == SpotifyConnectionState.Connected
            ? localizer.GetText("DisconnectSpotify")
            : localizer.GetText("ConnectSpotify");

        if (!IsConfigured)
        {
            StatusText = localizer.GetText("SpotifyDisconnected");
        }
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
