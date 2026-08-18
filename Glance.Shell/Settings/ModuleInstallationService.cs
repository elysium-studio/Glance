namespace Glance.Shell;

public sealed class ModuleInstallationService
{
    private readonly Dictionary<string, ModuleRegistration> packages = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, ModuleRegistration> registrations = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly object synchronization = new();
    private Func<string, Task<ModuleInstallResult>>? install;

    public void ConfigureInstaller(Func<string, Task<ModuleInstallResult>> installer)
    {
        ArgumentNullException.ThrowIfNull(installer);

        lock (synchronization)
        {
            install = installer;
        }
    }

    public Task<ModuleInstallResult> InstallAsync(string packagePath)
    {
        Func<string, Task<ModuleInstallResult>>? installer;

        lock (synchronization)
        {
            installer = install;
        }

        return installer?.Invoke(packagePath)
            ?? Task.FromResult(ModuleInstallResult.Failed("The module installer is not available."));
    }

    public bool CanUninstall(string componentId)
    {
        lock (synchronization)
        {
            return registrations.ContainsKey(componentId);
        }
    }

    public bool CanUninstallPackage(string packageId)
    {
        lock (synchronization)
        {
            return packages.ContainsKey(packageId);
        }
    }

    public void Register(string packageId, IEnumerable<string> componentIds, Func<Task<bool>> uninstall)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(componentIds);
        ArgumentNullException.ThrowIfNull(uninstall);

        ModuleRegistration registration = new(packageId, uninstall);

        lock (synchronization)
        {
            packages[packageId] = registration;

            foreach (string componentId in componentIds)
            {
                registrations[componentId] = registration;
            }
        }
    }

    public void Unregister(string packageId, IEnumerable<string> componentIds)
    {
        lock (synchronization)
        {
            _ = packages.Remove(packageId);

            foreach (string componentId in componentIds)
            {
                _ = registrations.Remove(componentId);
            }
        }
    }

    public Task<bool> UninstallAsync(string componentId)
    {
        ModuleRegistration? registration;

        lock (synchronization)
        {
            _ = registrations.TryGetValue(componentId, out registration);
        }

        return registration?.Uninstall() ?? Task.FromResult(false);
    }

    public Task<bool> UninstallPackageAsync(string packageId)
    {
        ModuleRegistration? registration;

        lock (synchronization)
        {
            _ = packages.TryGetValue(packageId, out registration);
        }

        return registration?.Uninstall() ?? Task.FromResult(false);
    }
}
