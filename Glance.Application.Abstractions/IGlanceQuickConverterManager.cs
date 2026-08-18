namespace Glance.Application.Abstractions;

public interface IGlanceQuickConverterManager
{
    event EventHandler? Changed;

    IReadOnlyList<GlanceQuickConverterExtension> GetConverters();

    Task SetEnabledAsync(string converterId, bool enabled, CancellationToken cancellationToken = default);

    Task<GlanceQuickConverterInstallResult> InstallAsync(string packagePath, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string converterId, CancellationToken cancellationToken = default);
}
