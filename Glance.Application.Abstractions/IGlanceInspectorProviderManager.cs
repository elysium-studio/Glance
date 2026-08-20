namespace Glance.Application.Abstractions;

public interface IGlanceInspectorProviderManager
{
    event EventHandler? Changed;

    IReadOnlyList<GlanceInspectorProviderExtension> GetProviders();

    Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default);

    Task<GlanceInspectorProviderInstallResult> InstallAsync(string packagePath, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default);
}
