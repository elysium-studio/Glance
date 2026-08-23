using Glance.Application.Abstractions;
using System.Security.Cryptography;

namespace Glance.Shell;

public sealed class GlanceModulePackageService :
    IGlanceModulePackageService
{
    private readonly Dictionary<string, string> activeDownloads = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IGlanceModuleDependencyResolver dependencies;
    private readonly IBackgroundDownloadManager downloads;
    private readonly ModuleInstallationService installations;
    private readonly string packageCacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Modules", "Feed", "Packages");
    private readonly object synchronization = new();

    public GlanceModulePackageService(IBackgroundDownloadManager downloads, IGlanceModuleDependencyResolver dependencies, ModuleInstallationService installations)
    {
        this.downloads = downloads;
        this.dependencies = dependencies;
        this.installations = installations;
    }

    public async Task<ModuleInstallResult> InstallAsync(GlanceModuleFeedItem module, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GlanceModuleFeedItem> requiredModules;

        try
        {
            requiredModules = dependencies.Resolve(module);
        }
        catch (InvalidDataException exception)
        {
            return ModuleInstallResult.Failed(exception.Message);
        }

        bool requiresRestart = false;

        foreach (GlanceModuleFeedItem requiredModule in requiredModules)
        {
            ModuleInstallResult dependencyResult = await InstallPackageAsync(requiredModule, cancellationToken);

            if (!dependencyResult.IsSuccessful)
            {
                return dependencyResult;
            }

            requiresRestart |= dependencyResult.RequiresRestart;
        }

        ModuleInstallResult result = await InstallPackageAsync(module, cancellationToken);
        return requiresRestart && result.IsSuccessful ? result with { RequiresRestart = true } : result;
    }

    public bool Cancel(string moduleId)
    {
        lock (synchronization)
        {
            return activeDownloads.TryGetValue(moduleId, out string? downloadId) && downloads.Cancel(downloadId);
        }
    }

    private async Task<ModuleInstallResult> InstallPackageAsync(GlanceModuleFeedItem module, CancellationToken cancellationToken)
    {
        if (module.IsDelisted || module.IsRevoked)
        {
            return ModuleInstallResult.Failed("This module is not currently available.");
        }

        if (!module.IsCompatible)
        {
            return ModuleInstallResult.Failed("This module requires a different version of Glance.");
        }

        string packageDirectory = Path.Combine(packageCacheDirectory, module.Id, module.Version);
        string packagePath = Path.Combine(packageDirectory, $"{module.Id}.glance");
        _ = Directory.CreateDirectory(packageDirectory);
        File.Delete(packagePath);
        string downloadId = GetDownloadId(module);

        lock (synchronization)
        {
            activeDownloads[module.Id] = downloadId;
        }

        BackgroundDownloadSnapshot download;

        try
        {
            _ = downloads.Enqueue(new BackgroundDownloadRequest(downloadId, module.DownloadUrl, packagePath));
            download = await downloads.WaitForCompletionAsync(downloadId, cancellationToken);
        }
        finally
        {
            lock (synchronization)
            {
                _ = activeDownloads.Remove(module.Id);
            }
        }

        if (download.Status != BackgroundDownloadStatus.Completed)
        {
            return ModuleInstallResult.Failed(download.ErrorMessage ?? "The module could not be downloaded.");
        }

        string hash;

        await using (FileStream packageStream = File.OpenRead(packagePath))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(packageStream, cancellationToken));
        }

        if (!string.Equals(hash, module.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(packagePath);
            return ModuleInstallResult.Failed("The downloaded module did not pass verification.");
        }

        ModuleInstallResult result = await installations.InstallAsync(packagePath);

        if (result.IsSuccessful)
        {
            installations.SetInstalledVersion(module.Id, module.Version);
        }

        return result;
    }

    private static string GetDownloadId(GlanceModuleFeedItem module) => $"glance-module:{module.Id}:{module.Version}";
}
