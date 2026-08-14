using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace Glance.Spotify.WinUI;

internal interface ISpotifyCredentialStore
{
    Task<SpotifyStoredCredential?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(SpotifyStoredCredential credential,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

internal sealed class SpotifyCredentialStore : ISpotifyCredentialStore
{
    private const string ResourceName = "Glance.Spotify";

    private readonly PasswordVault vault = new();

    public Task<SpotifyStoredCredential?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            PasswordCredential? credential = vault.FindAllByResource(ResourceName).FirstOrDefault();

            if (credential is null)
            {
                return Task.FromResult<SpotifyStoredCredential?>(null);
            }

            credential.RetrievePassword();
            return Task.FromResult<SpotifyStoredCredential?>(new SpotifyStoredCredential(credential.UserName,
                credential.Password));
        }
        catch
        {
            return Task.FromResult<SpotifyStoredCredential?>(null);
        }
    }

    public Task WriteAsync(SpotifyStoredCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Clear();
        vault.Add(new PasswordCredential(ResourceName, credential.ClientId, credential.RefreshToken));
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Clear();
        return Task.CompletedTask;
    }

    private void Clear()
    {
        try
        {
            foreach (PasswordCredential credential in vault.FindAllByResource(ResourceName))
            {
                vault.Remove(credential);
            }
        }
        catch
        {
        }
    }
}
