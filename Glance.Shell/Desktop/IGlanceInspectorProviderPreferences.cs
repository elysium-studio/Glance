namespace Glance.Shell;

public interface IGlanceInspectorProviderPreferences
{
    bool IsEnabled(string providerId);

    Task RegisterAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default);

    Task RemoveAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default);
}
