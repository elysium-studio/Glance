using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceQuickConverterManager :
    IGlanceQuickConverterManager
{
    private readonly ModuleInstallationService installations;
    private readonly IGlanceQuickConverterPreferences preferences;
    private readonly GlanceQuickConverterRegistry registry;

    public GlanceQuickConverterManager(GlanceQuickConverterRegistry registry, IGlanceQuickConverterPreferences preferences, ModuleInstallationService installations)
    {
        this.registry = registry;
        this.preferences = preferences;
        this.installations = installations;
        registry.Changed += HandleRegistryChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<GlanceQuickConverterExtension> GetConverters() => [.. registry.GetRegistrations().Select(registration => new GlanceQuickConverterExtension(registration.Converter.Descriptor.Id, registration.Converter.Descriptor.DisplayName, registration.Converter.Descriptor.Description, preferences.IsEnabled(registration.Converter.Descriptor.Id), registration.PackageId is not null && installations.CanUninstallPackage(registration.PackageId)))];

    public async Task SetEnabledAsync(string converterId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!registry.GetRegistrations().Any(registration => string.Equals(registration.Converter.Descriptor.Id, converterId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("The converter is not installed.", nameof(converterId));
        }

        await preferences.SetEnabledAsync(converterId, enabled, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<GlanceQuickConverterInstallResult> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ModuleInstallResult result = await installations.InstallAsync(packagePath);

        if (!result.IsSuccessful)
        {
            return GlanceQuickConverterInstallResult.Failed(result.ErrorMessage ?? "ConverterInstallFailed");
        }

        if (result.QuickConverterIds.Count > 0 && result.ComponentIds.Count == 0)
        {
            return GlanceQuickConverterInstallResult.Installed(result.RequiresRestart);
        }

        if (result.PackageId is not null)
        {
            _ = await installations.UninstallPackageAsync(result.PackageId);
        }

        return GlanceQuickConverterInstallResult.Failed("IncompatibleConverterPackage");
    }

    public async Task<bool> RemoveAsync(string converterId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GlanceQuickConverterRegistration[] registrations = [.. registry.GetRegistrations()];
        string? packageId = registrations
            .Where(registration => string.Equals(registration.Converter.Descriptor.Id, converterId, StringComparison.OrdinalIgnoreCase))
            .Select(registration => registration.PackageId)
            .FirstOrDefault();

        if (packageId is null || !await installations.UninstallPackageAsync(packageId))
        {
            return false;
        }

        return true;
    }

    private void HandleRegistryChanged(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
}
