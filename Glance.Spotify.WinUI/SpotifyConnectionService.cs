using Glance.Spotify;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

internal interface ISpotifyAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    void InvalidateAccessToken();
}

internal sealed class SpotifyConnectionService(ISpotifyAuthorizationBroker authorizationBroker,
    SpotifyOAuthClient oauthClient,
    ISpotifyCredentialStore credentialStore) :
    ISpotifyConnectionService,
    ISpotifyAccessTokenProvider,
    IDisposable
{
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private SpotifyAccessToken? accessToken;
    private int disposed;

    public SpotifyConnectionState State { get; private set; }

    public string? ConnectedClientId { get; private set; }

    public event EventHandler<SpotifyConnectionStateChangedEventArgs>? StateChanged;

    public async Task<SpotifyConnectionResult> ConnectAsync(string clientId,
        CancellationToken cancellationToken = default)
    {
        string normalizedClientId = clientId.Trim();

        if (!SpotifyClientIdValidator.IsValid(normalizedClientId))
        {
            return new SpotifyConnectionResult(false, "Enter a valid Spotify Client ID.");
        }

        await synchronization.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            SetState(SpotifyConnectionState.Connecting);
            SpotifyAuthorizationGrant grant = await authorizationBroker.AuthorizeAsync(normalizedClientId,
                cancellationToken);
            SpotifyAccessToken token = await oauthClient.ExchangeAsync(normalizedClientId,
                grant,
                cancellationToken);
            await credentialStore.WriteAsync(new SpotifyStoredCredential(normalizedClientId,
                token.RefreshToken),
                cancellationToken);
            accessToken = token;
            ConnectedClientId = normalizedClientId;
            SetState(SpotifyConnectionState.Connected);
            return new SpotifyConnectionResult(true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(SpotifyConnectionState.Disconnected);
            return new SpotifyConnectionResult(false, "Spotify sign-in timed out.");
        }
        catch (OperationCanceledException)
        {
            SetState(SpotifyConnectionState.Disconnected);
            throw;
        }
        catch (Exception exception)
        {
            accessToken = null;
            ConnectedClientId = null;
            SetState(SpotifyConnectionState.Error, exception.Message);
            return new SpotifyConnectionResult(false, exception.Message);
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task<bool> RestoreAsync(string clientId,
        CancellationToken cancellationToken = default)
    {
        string normalizedClientId = clientId.Trim();

        if (!SpotifyClientIdValidator.IsValid(normalizedClientId))
        {
            SetState(SpotifyConnectionState.Disconnected);
            return false;
        }

        await synchronization.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            if (State == SpotifyConnectionState.Connected &&
                string.Equals(ConnectedClientId, normalizedClientId, StringComparison.Ordinal) &&
                accessToken?.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return true;
            }

            SpotifyStoredCredential? credential = await credentialStore.ReadAsync(cancellationToken);

            if (credential is null ||
                !string.Equals(credential.ClientId, normalizedClientId, StringComparison.Ordinal))
            {
                accessToken = null;
                ConnectedClientId = null;
                SetState(SpotifyConnectionState.Disconnected);
                return false;
            }

            SpotifyAccessToken token = await oauthClient.RefreshAsync(normalizedClientId,
                credential.RefreshToken,
                cancellationToken);
            await credentialStore.WriteAsync(new SpotifyStoredCredential(normalizedClientId,
                token.RefreshToken),
                cancellationToken);
            accessToken = token;
            ConnectedClientId = normalizedClientId;
            SetState(SpotifyConnectionState.Connected);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            accessToken = null;
            ConnectedClientId = null;
            SetState(SpotifyConnectionState.Error, exception.Message);
            return false;
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            accessToken = null;
            ConnectedClientId = null;
            await credentialStore.ClearAsync(cancellationToken);
            SetState(SpotifyConnectionState.Disconnected);
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        string? clientId = ConnectedClientId;

        if (clientId is null)
        {
            throw new SpotifyAuthenticationException("Spotify is not connected.");
        }

        await synchronization.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            if (accessToken?.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return accessToken.Value;
            }

            SpotifyStoredCredential? credential = await credentialStore.ReadAsync(cancellationToken);

            if (credential is null ||
                !string.Equals(credential.ClientId, clientId, StringComparison.Ordinal))
            {
                accessToken = null;
                ConnectedClientId = null;
                SetState(SpotifyConnectionState.Disconnected);
                throw new SpotifyAuthenticationException("Spotify is not connected.");
            }

            SpotifyAccessToken token = await oauthClient.RefreshAsync(clientId,
                credential.RefreshToken,
                cancellationToken);
            await credentialStore.WriteAsync(new SpotifyStoredCredential(clientId, token.RefreshToken),
                cancellationToken);
            accessToken = token;
            SetState(SpotifyConnectionState.Connected);
            return token.Value;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            accessToken = null;
            ConnectedClientId = null;
            SetState(SpotifyConnectionState.Error, exception.Message);
            throw;
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public void InvalidateAccessToken()
    {
        if (accessToken is not null)
        {
            accessToken = accessToken with { ExpiresAt = DateTimeOffset.MinValue };
        }
    }

    public void Dispose()
    {
        _ = Interlocked.Exchange(ref disposed, 1);
    }

    private void SetState(SpotifyConnectionState state, string? errorMessage = null)
    {
        State = state;
        StateChanged?.Invoke(this, new SpotifyConnectionStateChangedEventArgs(state, errorMessage));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
}
