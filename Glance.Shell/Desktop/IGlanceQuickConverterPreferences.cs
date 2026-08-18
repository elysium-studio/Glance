namespace Glance.Shell;

public interface IGlanceQuickConverterPreferences
{
    bool IsEnabled(string converterId);

    Task RegisterAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default);

    Task RemoveAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string converterId, bool enabled, CancellationToken cancellationToken = default);
}
