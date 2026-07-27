using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace Glance.ThemeSwitcher.WinUI;

public sealed class WindowsLocationService
{
    public async Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();

        if (access != GeolocationAccessStatus.Allowed)
        {
            return null;
        }

        Geolocator locator = new()
        {
            DesiredAccuracy = PositionAccuracy.Default,
            MovementThreshold = 1000
        };
        Geoposition position = await locator.GetGeopositionAsync(TimeSpan.FromHours(12), TimeSpan.FromSeconds(12)).AsTask(cancellationToken);
        return (position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
    }
}
