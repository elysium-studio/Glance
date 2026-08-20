using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceInspectorProviderManager :
    IGlanceInspectorProviderManager
{
    private readonly ModuleInstallationService installations;
    private readonly IGlanceInspectorProviderPreferences preferences;
    private readonly GlanceInspectorProviderRegistry registry;

    public GlanceInspectorProviderManager(GlanceInspectorProviderRegistry registry, IGlanceInspectorProviderPreferences preferences, ModuleInstallationService installations)
    {
        this.registry = registry;
        this.preferences = preferences;
        this.installations = installations;
        registry.Changed += HandleRegistryChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<GlanceInspectorProviderExtension> GetProviders() => [.. registry.GetRegistrations().Select(registration => new GlanceInspectorProviderExtension(registration.Provider.Descriptor.Id, registration.Provider.Descriptor.DisplayName, registration.Provider.Descriptor.Description, preferences.IsEnabled(registration.Provider.Descriptor.Id), registration.PackageId is not null && installations.CanUninstallPackage(registration.PackageId)))];

    public async Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!registry.GetRegistrations().Any(registration => string.Equals(registration.Provider.Descriptor.Id, providerId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The inspector provider is not installed.", nameof(providerId));
        }

        await preferences.SetEnabledAsync(providerId, enabled, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<GlanceInspectorProviderInstallResult> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ModuleInstallResult result = await installations.InstallAsync(packagePath);

        if (!result.IsSuccessful)
        {
            return GlanceInspectorProviderInstallResult.Failed(result.ErrorMessage ?? "InspectorProviderInstallFailed");
        }

        if (result.InspectorProviderIds.Count > 0 && result.ComponentIds.Count == 0 && result.QuickConverterIds.Count == 0)
        {
            return GlanceInspectorProviderInstallResult.Installed(result.RequiresRestart);
        }

        if (result.PackageId is not null)
        {
            _ = await installations.UninstallPackageAsync(result.PackageId);
        }

        return GlanceInspectorProviderInstallResult.Failed("IncompatibleInspectorProviderPackage");
    }

    public async Task<bool> RemoveAsync(string providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? packageId = registry.GetRegistrations().Where(registration => string.Equals(registration.Provider.Descriptor.Id, providerId, StringComparison.OrdinalIgnoreCase)).Select(registration => registration.PackageId).FirstOrDefault();
        return packageId is not null && await installations.UninstallPackageAsync(packageId);
    }

    private void HandleRegistryChanged(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
}
