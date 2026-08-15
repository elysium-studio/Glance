using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Glance.Spotify.WinUI;

public interface ISpotifySetupService
{
    string RedirectUri { get; }

    Task OpenDashboardAsync();

    Task<bool> CopyRedirectUriAsync();
}

internal sealed class SpotifySetupService : ISpotifySetupService
{
    private static readonly Uri DashboardUri = new("https://developer.spotify.com/dashboard");

    public string RedirectUri => SpotifyAuthenticationDefaults.RedirectUri.AbsoluteUri;

    public async Task OpenDashboardAsync() => _ = await Launcher.LaunchUriAsync(DashboardUri);

    public async Task<bool> CopyRedirectUriAsync()
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                DataPackage content = new();
                content.SetText(RedirectUri);
                Clipboard.SetContent(content);
                return true;
            }
            catch (COMException)
            {
                if (attempt == 5)
                {
                    return false;
                }

                await Task.Delay(40 * (attempt + 1));
            }
        }

        return false;
    }
}
