namespace Glance.Shell;

public sealed class ModuleInstallationService
{
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

    public void Register(string packageId,
        IEnumerable<string> componentIds,
        Func<Task<bool>> uninstall)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(componentIds);
        ArgumentNullException.ThrowIfNull(uninstall);

        ModuleRegistration registration = new(packageId, uninstall);

        lock (synchronization)
        {
            foreach (string componentId in componentIds)
            {
                registrations[componentId] = registration;
            }
        }
    }

    public void Unregister(IEnumerable<string> componentIds)
    {
        lock (synchronization)
        {
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

    private sealed record ModuleRegistration(string PackageId,
        Func<Task<bool>> Uninstall);
}

public sealed record ModuleInstallResult(bool IsSuccessful,
    IReadOnlyList<string> ComponentIds,
    bool RequiresRestart,
    string? ErrorMessage)
{
    public static ModuleInstallResult Installed(IEnumerable<string> componentIds) => new(true,
        [.. componentIds],
        false,
        null);

    public static ModuleInstallResult Staged(IEnumerable<string> componentIds) => new(true,
        [.. componentIds],
        true,
        null);

    public static ModuleInstallResult Failed(string message) => new(false,
        [],
        false,
        message);
}
